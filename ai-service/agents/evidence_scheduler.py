"""Deterministic scheduling of verified evidence; this module never writes farm data."""

from datetime import date, datetime, time, timedelta, timezone
from decimal import Decimal, InvalidOperation
from uuid import UUID

from schemas.scheduling_validation import (
    CandidateIrrigation, CandidateReservation, CandidateTask, SchedulingConstraint,
    SchedulingSource, SchedulingValidationInput, SchedulingValidationOutput,
)
from tools.scheduling_tools import SchedulingTools


def guid(value: object) -> UUID | None:
    try:
        return UUID(str(value))
    except (ValueError, TypeError):
        return None


def quantity(value: object) -> Decimal | None:
    try:
        amount = Decimal(str(value))
        return amount if amount.is_finite() and amount > 0 and amount.as_tuple().exponent >= -3 else None
    except (InvalidOperation, ValueError):
        return None


class EvidenceScheduler:
    @staticmethod
    def result(request: SchedulingValidationInput, status: str, warnings: list[str],
               tasks: list[CandidateTask], irrigation: list[CandidateIrrigation],
               reservations: list[CandidateReservation], constraints: list[SchedulingConstraint]) -> SchedulingValidationOutput:
        return SchedulingValidationOutput(
            workflowId=str(request.workflow_id), candidateRevision=request.candidate_revision,
            status=status, requiresHumanReview=True, requiresHumanApproval=status == "CandidateReady",
            warnings=warnings, candidateTasks=tasks, candidateIrrigation=irrigation,
            candidateReservations=reservations, estimatedCost=None, constraints=constraints,
        )

    @classmethod
    def run(cls, request: SchedulingValidationInput) -> SchedulingValidationOutput:
        missing = cls.check_dependencies(request)
        if missing is not None:
            return missing
        return cls.assess_risk(request, cls.propose(request))

    @classmethod
    def check_dependencies(cls, request: SchedulingValidationInput) -> SchedulingValidationOutput | None:
        evidence = request.evidence
        if evidence is None:
            return cls.result(request, "MissingDependency", ["Verified crop reference evidence is unavailable."],
                              [], [], [], [SchedulingConstraint(code="UPSTREAM_DEPENDENCY", severity="Blocking",
                              message="Scheduling requires persisted and verified reference evidence.")])
        missing = []
        for output, expected, label in (
            (request.coordinator_output, "Planned", "Crop planning"),
            (request.field_analysis_output, "Analyzed", "Field analysis"),
            (request.weather_resource_output, "Analyzed", "Weather/resource analysis"),
        ):
            if output.get("status") != expected or str(output.get("workflowId")) != str(request.workflow_id):
                missing.append(f"{label} is missing, incomplete, or from another workflow.")
        if request.field_id is None or request.preferred_end_date < request.preferred_start_date:
            missing.append("A valid field and scheduling window are required.")
        if not evidence.profile_id or not evidence.verified_at or not evidence.stages:
            missing.append("A verified crop profile with stages is required.")
        if not all((evidence.coordinator_step_id, evidence.field_analysis_step_id, evidence.weather_resource_step_id)):
            missing.append("Persisted upstream step IDs are required.")
        source = request.weather_resource_output.get("requirementSource")
        if isinstance(source, dict) and source.get("cropReferenceProfileId") is not None:
            if str(source["cropReferenceProfileId"]).lower() != str(evidence.profile_id).lower():
                missing.append("Member 3 and scheduling use different crop reference profiles.")
        if missing:
            return cls.result(request, "MissingDependency", missing, [], [], [], [
                SchedulingConstraint(code="UPSTREAM_DEPENDENCY", severity="Blocking", message="Matching upstream and verified reference evidence are required.")
            ])
        return None

    @classmethod
    def propose(cls, request: SchedulingValidationInput) -> SchedulingValidationOutput:
        evidence = request.evidence
        assert evidence is not None
        warnings = ["No verified price contract exists; estimated cost is unknown."]
        for output in (request.field_analysis_output, request.weather_resource_output):
            if isinstance(output.get("warnings"), list):
                warnings.extend(item for item in output["warnings"] if isinstance(item, str) and item)
        blocking: list[str] = []
        if evidence.invalid_irrigation_rule_ids:
            blocking.append("A persisted irrigation rule is invalid and must be corrected before approval.")
        rule_keys = [rule.rule_key.casefold() for rule in evidence.irrigation_rules]
        rule_slots = [(rule.day_offset_from_planting, rule.start_time_utc) for rule in evidence.irrigation_rules]
        if len(set(rule_keys)) != len(rule_keys) or len(set(rule_slots)) != len(rule_slots):
            blocking.append("Verified irrigation rules contain duplicate keys or slots.")
        constraints = [
            SchedulingConstraint(code="DATE_WINDOW", severity="Blocking", message="Every proposed item must fit inside the selected window."),
            SchedulingConstraint(code="HUMAN_APPROVAL", severity="Blocking", message="No candidate becomes final before officer approval."),
        ]
        tasks: list[CandidateTask] = []
        irrigation: list[CandidateIrrigation] = []
        occupied_tasks: set[datetime] = set()
        first_day = max(request.preferred_start_date, datetime.now(timezone.utc).date())
        if first_day > request.preferred_end_date:
            blocking.append("The scheduling window is in the past.")

        preparations = request.field_analysis_output.get("fieldPreparationRequirements", [])
        if not isinstance(preparations, list) or len(preparations) > 50:
            blocking.append("Field preparation evidence is invalid or exceeds the task limit.")
            preparations = []
        for item in preparations:
            if not isinstance(item, str) or not item.strip():
                blocking.append("A field preparation requirement is invalid.")
                continue
            slot = cls.task_slot(request, first_day, occupied_tasks)
            if slot is None:
                blocking.append("No same-day preparation task slot is available.")
                continue
            occupied_tasks.add(slot)
            tasks.append(CandidateTask(
                farmId=request.farm_id, title="Review recorded field preparation requirement",
                description="Review this field-officer requirement before field work.", dueAt=slot,
                assignedToUserId=request.assigned_to_user_id,
                reason=f"Member 2 recorded: {item.strip()[:350]}",
                sources=[SchedulingSource(kind="FieldAnalysis", id=evidence.field_analysis_step_id,
                                          label="Field Officer analysis")],
            ))

        planting_day = first_day + timedelta(days=1 if tasks else 0)
        stages = sorted(evidence.stages, key=lambda item: (item.sequence, str(item.id)))
        if len(stages) > 50 or len({stage.sequence for stage in stages}) != len(stages):
            blocking.append("Crop stages exceed the limit or have duplicate sequence values.")
        stage_day = planting_day
        for index, stage in enumerate(stages[:50]):
            if index:
                previous = stages[index - 1]
                if previous.typical_min_days is None or previous.typical_min_days < 0:
                    blocking.append(f"Stage {previous.id} lacks a valid minimum duration.")
                    break
                stage_day += timedelta(days=previous.typical_min_days)
            if stage_day > request.preferred_end_date or stage_day < first_day:
                blocking.append(f"Stage {stage.id} cannot fit inside the selected window.")
                break
            slot = cls.task_slot(request, stage_day, occupied_tasks)
            if slot is None:
                blocking.append(f"Stage {stage.id} has no same-day task slot.")
                break
            occupied_tasks.add(slot)
            tasks.append(CandidateTask(
                farmId=request.farm_id, title=f"Review {stage.stage_name[:120]} stage",
                description="Review the verified crop-cycle stage before field work.", dueAt=slot,
                assignedToUserId=request.assigned_to_user_id,
                reason=f"Verified stage {stage.sequence} follows the profile's minimum stage durations.",
                sources=[SchedulingSource(kind="CropStage", id=stage.id,
                    label=stage.source_name[:180] or "Verified crop stage", profileId=evidence.profile_id,
                    sourceVersion=evidence.source_version, verifiedAt=evidence.verified_at,
                    sourceUrl=stage.source_url or evidence.source_url)],
            ))
        if not any(source.kind == "CropStage" for task in tasks for source in task.sources):
            blocking.append("No verified crop-stage task fits inside the window.")
        if len(tasks) > 50:
            blocking.append("The task count exceeds the proposal limit.")

        occupied_irrigation: list[tuple[datetime, datetime]] = []
        if not evidence.irrigation_rules:
            warnings.append("No verified irrigation schedule rule exists; no irrigation was proposed.")
        if len(evidence.irrigation_rules) > 20:
            blocking.append("The irrigation rule count exceeds the proposal limit.")
        for rule in evidence.irrigation_rules[:20]:
            day = planting_day + timedelta(days=rule.day_offset_from_planting)
            try:
                clock = time.fromisoformat(rule.start_time_utc)
                if len(rule.start_time_utc) != 5 or clock.tzinfo is not None:
                    raise ValueError()
            except ValueError:
                blocking.append(f"Irrigation rule {rule.id} has invalid UTC time.")
                continue
            if day < first_day or day > request.preferred_end_date:
                blocking.append(f"Irrigation rule {rule.id} cannot fit inside the selected window.")
                continue
            start = datetime.combine(day, clock, tzinfo=timezone.utc)
            if start <= datetime.now(timezone.utc):
                blocking.append(f"Irrigation rule {rule.id} falls in the past.")
                continue
            day_end = datetime.combine(day, time.max, tzinfo=timezone.utc)
            slot = SchedulingTools.next_irrigation_slot(start, day_end, request.field_id,
                rule.duration_minutes, request.existing_irrigation)
            while slot is not None and any(begin < slot + timedelta(minutes=rule.duration_minutes) and end > slot
                                           for begin, end in occupied_irrigation):
                slot = SchedulingTools.next_irrigation_slot(slot + timedelta(hours=1), day_end,
                    request.field_id, rule.duration_minutes, request.existing_irrigation)
            if slot is None:
                blocking.append(f"Irrigation rule {rule.id} has no same-day conflict-free slot.")
                continue
            occupied_irrigation.append((slot, slot + timedelta(minutes=rule.duration_minutes)))
            irrigation.append(CandidateIrrigation(
                fieldId=request.field_id, scheduledAt=slot, durationMinutes=rule.duration_minutes,
                notes="Candidate only; officer approval is required.",
                reason=f"Verified irrigation rule {rule.rule_key[:100]} specifies this offset, UTC time and duration.",
                sources=[SchedulingSource(kind="IrrigationRule", id=rule.id,
                    label=rule.source_name[:180] or "Verified irrigation rule", profileId=evidence.profile_id,
                    sourceVersion=evidence.source_version, verifiedAt=rule.verified_at,
                    sourceUrl=rule.source_url or evidence.source_url)],
            ))

        reservations, errors = cls.reservations(request)
        blocking.extend(errors)
        if blocking:
            reservations = []
            constraints.extend(SchedulingConstraint(code="EVIDENCE_BLOCK", severity="Blocking", message=message)
                               for message in dict.fromkeys(blocking))
        return cls.result(request, "CandidateBlocked" if blocking else "CandidateReady",
                          warnings + blocking, tasks, irrigation, reservations, constraints)

    @staticmethod
    def assess_risk(request: SchedulingValidationInput, draft: SchedulingValidationOutput) -> SchedulingValidationOutput:
        risk = request.weather_resource_output.get("weatherRisk", "Unknown")
        if risk == "High":
            draft.status = "CandidateBlocked"
            draft.requires_human_approval = False
            draft.candidate_reservations = []
            message = "High weather risk blocks approval until a new upstream analysis."
            draft.warnings.append(message)
            draft.constraints.append(SchedulingConstraint(code="HIGH_WEATHER_RISK", severity="Blocking", message=message))
        elif risk == "Unknown":
            draft.warnings.append("Weather risk is unknown; the officer must review missing forecast evidence.")
        elif risk not in {"Low", "Medium"}:
            draft.status = "CandidateBlocked"
            draft.requires_human_approval = False
            draft.candidate_reservations = []
            message = "The persisted weather risk value is invalid."
            draft.warnings.append(message)
            draft.constraints.append(SchedulingConstraint(code="INVALID_WEATHER_RISK", severity="Blocking", message=message))
        return draft

    @staticmethod
    def task_slot(request: SchedulingValidationInput, day: date, occupied: set[datetime]) -> datetime | None:
        start = datetime.combine(day, time(hour=8), tzinfo=timezone.utc)
        if day == datetime.now(timezone.utc).date():
            now = datetime.now(timezone.utc)
            start = max(start, now.replace(minute=0, second=0, microsecond=0) + timedelta(hours=1))
        end = datetime.combine(day, time.max, tzinfo=timezone.utc)
        slot = SchedulingTools.next_task_slot(start, end, request.assigned_to_user_id, request.existing_tasks)
        while slot in occupied:
            slot = SchedulingTools.next_task_slot(slot + timedelta(hours=1), end,
                request.assigned_to_user_id, request.existing_tasks)
        return slot

    @staticmethod
    def reservations(request: SchedulingValidationInput) -> tuple[list[CandidateReservation], list[str]]:
        output = request.weather_resource_output
        requirements, checks = output.get("resourceRequirements"), output.get("resourceChecks")
        if output.get("requirementStatus") != "Sufficient":
            return [], ["Verified resource requirements are not sufficient for approval."]
        if not isinstance(requirements, list) or not requirements or not isinstance(checks, list):
            return [], ["Verified resource requirements or stock checks are unavailable."]
        if len(requirements) > 50:
            return [], ["The resource requirement count exceeds the proposal limit."]
        seen_resources: set[tuple[UUID, str]] = set()
        seen_stocks: set[UUID] = set()
        reservations: list[CandidateReservation] = []
        errors: list[str] = []
        for item in requirements:
            if not isinstance(item, dict):
                errors.append("A resource requirement has invalid shape.")
                continue
            rule_id, resource_id = guid(item.get("ruleId")), guid(item.get("resourceId"))
            unit, amount = item.get("unit"), quantity(item.get("requiredQuantity"))
            if not rule_id or not resource_id or not isinstance(unit, str) or not unit.strip() or amount is None:
                errors.append("A resource requirement lacks a verified rule, resource, unit or positive quantity.")
                continue
            identity = (resource_id, unit.casefold())
            if identity in seen_resources:
                errors.append("Duplicate resource requirements cannot be reserved twice.")
            seen_resources.add(identity)
            if item.get("requirementStatus") != "Sufficient" or item.get("sufficient") is not True:
                errors.append("A resource requirement is not sufficient.")
                continue
            matches = [check for check in checks if isinstance(check, dict) and
                       str(check.get("resourceId", "")).lower() == str(resource_id).lower() and
                       str(check.get("unit", "")).casefold() == unit.casefold()]
            if len(matches) != 1:
                errors.append("A resource requirement has no unique stock/unit match.")
                continue
            check = matches[0]
            stock_id = guid(check.get("inventoryStockId"))
            available, requested = quantity(check.get("availableQuantity")), quantity(check.get("requested"))
            if (stock_id is None or stock_id in seen_stocks or available is None or available < amount or
                    requested != amount or check.get("requirementStatus") != "Sufficient" or
                    check.get("sufficient") is not True):
                errors.append("A stock row is missing, duplicated or insufficient.")
                continue
            seen_stocks.add(stock_id)
            evidence = request.evidence
            assert evidence is not None
            reservations.append(CandidateReservation(
                inventoryStockId=stock_id, quantity=float(amount), purpose="Candidate crop-plan resource use",
                estimatedUnitCost=None,
                reason=f"Member 3 verified {amount} {unit[:40]} required and available.",
                sources=[SchedulingSource(kind="ResourceRequirement", id=rule_id,
                    label=str(item.get("resourceName") or "Verified resource requirement")[:180],
                    profileId=evidence.profile_id, sourceVersion=evidence.source_version,
                    verifiedAt=evidence.verified_at, sourceUrl=evidence.source_url)],
            ))
        return ([] if errors else reservations), errors
