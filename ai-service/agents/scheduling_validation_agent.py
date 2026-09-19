from datetime import datetime, time, timedelta, timezone

from schemas.scheduling_validation import (
    CandidateIrrigation,
    CandidateTask,
    SchedulingConstraint,
    SchedulingValidationInput,
    SchedulingValidationOutput,
)
from tools.scheduling_tools import SchedulingTools


class SchedulingValidationAgent:
    """Builds deterministic proposals from persisted Member 1-3 results."""

    async def run(self, request: SchedulingValidationInput) -> SchedulingValidationOutput:
        missing = self._missing_dependencies(request)
        if missing:
            return SchedulingValidationOutput(
                workflowId=str(request.workflow_id),
                candidateRevision=request.candidate_revision,
                status="MissingDependency",
                requiresHumanReview=True,
                requiresHumanApproval=False,
                warnings=missing,
                candidateTasks=[],
                candidateIrrigation=[],
                candidateReservations=[],
                estimatedCost=None,
                constraints=[
                    SchedulingConstraint(
                        code="UPSTREAM_DEPENDENCY",
                        severity="Blocking",
                        message="All persisted upstream steps must complete for the same workflow before scheduling.",
                    )
                ],
            )

        window_start, window_end = SchedulingTools.date_window(
            request.preferred_start_date, request.preferred_end_date
        )
        task_start = datetime.combine(
            request.preferred_start_date, time(hour=8), tzinfo=timezone.utc
        )
        task_at = SchedulingTools.next_task_slot(
            task_start, window_end, request.assigned_to_user_id, request.existing_tasks
        )

        irrigation_start = datetime.combine(
            request.preferred_start_date, time(hour=6), tzinfo=timezone.utc
        ) + timedelta(days=1)
        if irrigation_start > window_end:
            irrigation_start = datetime.combine(
                request.preferred_start_date, time(hour=6), tzinfo=timezone.utc
            )
        irrigation_at = SchedulingTools.next_irrigation_slot(
            irrigation_start,
            window_end,
            request.field_id,
            60,
            request.existing_irrigation,
        )

        warnings: list[str] = []
        constraints = [
            SchedulingConstraint(
                code="DATE_WINDOW",
                severity="Blocking",
                message=f"Candidate work must remain between {request.preferred_start_date} and {request.preferred_end_date}.",
            ),
            SchedulingConstraint(
                code="HUMAN_APPROVAL",
                severity="Blocking",
                message="No task, irrigation schedule, or reservation may be created before officer approval.",
            ),
        ]

        weather_risk = str(request.weather_resource_output.get("weatherRisk", "Unknown"))
        if weather_risk in {"High", "Unknown"}:
            warnings.append(f"Weather risk is {weather_risk}; the officer must confirm the proposed times.")
        warnings.append("No authoritative unit-cost input was supplied; estimated cost remains unknown.")
        warnings.extend(str(item) for item in request.weather_resource_output.get("warnings", []) if item)
        warnings.extend(str(item) for item in request.field_analysis_output.get("warnings", []) if item)

        if task_at is None or irrigation_at is None:
            unavailable = []
            if task_at is None:
                unavailable.append("No conflict-free task slot exists inside the requested date window.")
            if irrigation_at is None:
                unavailable.append("No conflict-free irrigation slot exists inside the requested date window.")
            return SchedulingValidationOutput(
                workflowId=str(request.workflow_id),
                candidateRevision=request.candidate_revision,
                status="MissingDependency",
                requiresHumanReview=True,
                requiresHumanApproval=False,
                warnings=warnings + unavailable,
                candidateTasks=[],
                candidateIrrigation=[],
                candidateReservations=[],
                estimatedCost=None,
                constraints=constraints,
            )

        priority = str(request.field_analysis_output.get("priority", "Unknown"))
        task = CandidateTask(
            farmId=request.farm_id,
            title="Review crop plan and field readiness",
            description=f"Review the approved crop objective and {priority.lower()} priority field evidence before field work begins.",
            dueAt=task_at,
            assignedToUserId=request.assigned_to_user_id,
        )
        irrigation = CandidateIrrigation(
            fieldId=request.field_id,
            scheduledAt=irrigation_at,
            durationMinutes=60,
            notes=f"Candidate irrigation generated from the verified plan window; stored weather risk: {weather_risk}.",
        )

        return SchedulingValidationOutput(
            workflowId=str(request.workflow_id),
            candidateRevision=request.candidate_revision,
            status="CandidateReady",
            requiresHumanReview=bool(warnings),
            requiresHumanApproval=True,
            warnings=warnings,
            candidateTasks=[task],
            candidateIrrigation=[irrigation],
            candidateReservations=[],
            estimatedCost=None,
            constraints=constraints,
        )

    @staticmethod
    def _missing_dependencies(request: SchedulingValidationInput) -> list[str]:
        checks = (
            (request.coordinator_output, "Planned", "Crop planning coordinator output is missing or not complete."),
            (request.field_analysis_output, "Analyzed", "Field analysis output is missing or not complete."),
            (request.weather_resource_output, "Analyzed", "Weather/resource output is missing or not complete."),
        )
        warnings = [message for output, expected, message in checks if output.get("status") != expected]
        for output, _, message in checks:
            if str(output.get("workflowId", "")) != str(request.workflow_id):
                warnings.append(message.replace("missing or not complete", "from a different workflow"))
        if request.field_id is None:
            warnings.append("A field is required to produce an irrigation candidate.")
        if request.preferred_end_date < request.preferred_start_date:
            warnings.append("The crop plan date window is invalid.")
        return list(dict.fromkeys(warnings))
