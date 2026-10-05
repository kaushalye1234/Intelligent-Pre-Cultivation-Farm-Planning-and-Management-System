import json
from datetime import datetime, time, timezone
from typing import Any
from uuid import UUID

from schemas.crop_planning import CropPlanContext, CropReferenceProfile
from schemas.scheduling_validation import (
    SchedulingEvidenceBundle,
    SchedulingIrrigationRuleEvidence,
    SchedulingStageEvidence,
)
from tools.crop_planning_tools import CropPlanningTools
from schemas.scheduling_validation import SchedulingValidationInput

MAX_OBSERVATION_BYTES = 16 * 1024


class SchedulingEvidenceTools:
    """The sole read-only retrieval action exposed to the scheduling agent."""

    def __init__(self, crop_planning_tools: CropPlanningTools) -> None:
        self._crop_planning_tools = crop_planning_tools

    async def get_verified_crop_profile(self, request: SchedulingValidationInput) -> SchedulingEvidenceBundle:
        context = await self._crop_planning_tools.get_crop_plan_context(
            request.crop_plan_request_id, request.workflow_id
        )
        self._check_size(context.model_dump(mode="json", by_alias=True))
        if context.id != request.crop_plan_request_id:
            raise ValueError("Crop plan context does not match the trusted request.")
        try:
            crop_type_context_id = UUID(str(context.crop_type.get("id")))
        except (AttributeError, TypeError, ValueError):
            raise ValueError("Crop plan context does not contain a valid crop type.") from None
        if context.crop_type.get("isActive") is not True or crop_type_context_id != context.crop_type_id:
            raise ValueError("Crop plan context does not contain the active requested crop type.")

        reference = await self._crop_planning_tools.get_crop_reference_profile(
            context.crop_type_id, request.workflow_id, context.crop_variety_name
        )
        self._check_size(reference.model_dump(mode="json", by_alias=True))
        return self._to_evidence(request, context, reference)

    @staticmethod
    def _check_size(value: Any) -> None:
        serialized = json.dumps(value, separators=(",", ":"), ensure_ascii=False, default=str).encode("utf-8")
        if len(serialized) > MAX_OBSERVATION_BYTES:
            raise ValueError("Verified crop-profile observation exceeded the configured size limit.")

    @classmethod
    def _to_evidence(
        cls,
        request: SchedulingValidationInput,
        context: CropPlanContext,
        reference: CropReferenceProfile,
    ) -> SchedulingEvidenceBundle:
        if reference.reference_data_status.casefold() != "available" or not reference.profile:
            raise ValueError("A verified crop reference profile is unavailable.")
        profile = reference.profile
        profile_id = cls._required(profile, "id")
        try:
            profile_id = UUID(str(profile_id))
        except (TypeError, ValueError):
            raise ValueError("Verified crop profile ID is invalid.") from None
        crop_type_id = cls._required(profile, "cropTypeId", "crop_type_id")
        if str(crop_type_id).casefold() != str(context.crop_type_id).casefold():
            raise ValueError("Verified crop profile does not match the persisted crop type.")
        variety = cls._get(profile, "varietyName", "variety_name")
        if (variety or None) != (context.crop_variety_name or None):
            raise ValueError("Verified crop profile does not match the persisted crop variety.")
        if not cls._get(profile, "isActive", "is_active"):
            raise ValueError("Verified crop profile is inactive.")
        source_name = cls._required_text(profile, "sourceName", "source_name")
        source_version = cls._required_text(profile, "sourceVersion", "source_version")
        if not source_name or not source_version:
            raise ValueError("Verified crop profile source metadata is incomplete.")
        verified_at = cls._date(cls._required(profile, "verifiedAt", "verified_at"))
        now = datetime.now(timezone.utc)
        if verified_at > now:
            raise ValueError("Verified crop profile has a future verification time.")

        if not reference.stages or len(reference.stages) > 50:
            raise ValueError("Verified crop profile must contain between one and fifty stages.")
        stages: list[SchedulingStageEvidence] = []
        for stage in reference.stages:
            sequence = cls._required(stage, "sequence")
            min_days = cls._get(stage, "typicalMinDays", "typical_min_days")
            max_days = cls._get(stage, "typicalMaxDays", "typical_max_days")
            if (isinstance(sequence, bool) or not isinstance(sequence, int) or
                    any(isinstance(value, bool) or (value is not None and not isinstance(value, int))
                        for value in (min_days, max_days))):
                raise ValueError("Verified crop stage timing must use whole-day integer values.")
            stage_name = str(cls._required(stage, "stageName", "stage_name")).strip()
            stage_source = str(cls._get(stage, "sourceName", "source_name") or source_name).strip()
            if not stage_name or not stage_source or sequence < 1:
                raise ValueError("Verified crop stage name, source, or sequence is invalid.")
            if ((min_days is not None and min_days < 0) or (max_days is not None and max_days < 0) or
                    (min_days is not None and max_days is not None and min_days > max_days)):
                raise ValueError("Verified crop stage duration is invalid.")
            stages.append(SchedulingStageEvidence.model_validate({
                "id": cls._required(stage, "id"),
                "stageName": stage_name,
                "sequence": sequence,
                "typicalMinDays": min_days,
                "typicalMaxDays": max_days,
                "sourceName": stage_source,
                "sourceUrl": cls._get(stage, "sourceUrl", "source_url") or cls._get(profile, "sourceUrl", "source_url"),
            }))
        if (len({item.id for item in stages}) != len(stages) or
                len({item.sequence for item in stages}) != len(stages)):
            raise ValueError("Verified crop profile contains duplicate stage IDs or sequences.")

        original = request.evidence
        if original and (original.irrigation_rules or original.invalid_irrigation_rule_ids):
            irrigation_rules = original.irrigation_rules
            invalid_irrigation_rule_ids = original.invalid_irrigation_rule_ids
        else:
            irrigation_rules, invalid_irrigation_rule_ids = cls._irrigation_rules(
                reference.rules, source_name, cls._get(profile, "sourceUrl", "source_url"), now
            )
        return SchedulingEvidenceBundle(
            profileId=profile_id,
            sourceName=source_name,
            sourceUrl=cls._get(profile, "sourceUrl", "source_url"),
            sourceVersion=source_version,
            verifiedAt=verified_at,
            coordinatorStepId=original.coordinator_step_id if original else None,
            fieldAnalysisStepId=original.field_analysis_step_id if original else None,
            weatherResourceStepId=original.weather_resource_step_id if original else None,
            stages=stages,
            irrigationRules=irrigation_rules,
            invalidIrrigationRuleIds=invalid_irrigation_rule_ids,
        )

    @classmethod
    def _irrigation_rules(cls, rules: list[dict[str, Any]], source_name: str,
                          source_url: str | None, now: datetime) -> tuple[list[SchedulingIrrigationRuleEvidence], list[UUID]]:
        irrigation: list[SchedulingIrrigationRuleEvidence] = []
        invalid_ids: list[UUID] = []
        for rule in rules:
            if str(cls._get(rule, "ruleType", "rule_type") or "").casefold() != "irrigationschedule":
                continue
            raw_id = cls._required(rule, "id")
            try:
                rule_id = UUID(str(raw_id))
            except (TypeError, ValueError):
                raise ValueError("Verified irrigation rule ID is invalid.") from None
            try:
                structured = cls._get(rule, "structuredValueJson", "structured_value_json")
                value = json.loads(structured) if isinstance(structured, str) else structured
                if not isinstance(value, dict):
                    raise ValueError()
                offset = value.get("dayOffsetFromPlanting")
                duration = value.get("durationMinutes")
                start_time = value.get("startTimeUtc")
                verified_at = cls._date(cls._required(rule, "verifiedAt", "verified_at"))
                if (verified_at > now or isinstance(offset, bool) or not isinstance(offset, int) or
                        offset < 0 or offset > 365 or isinstance(duration, bool) or
                        not isinstance(duration, int) or duration < 1 or duration > 1440 or
                        not isinstance(start_time, str) or len(start_time) != 5 or
                        time.fromisoformat(start_time).strftime("%H:%M") != start_time or
                        time.fromisoformat(start_time).tzinfo is not None):
                    raise ValueError()
                irrigation.append(SchedulingIrrigationRuleEvidence.model_validate({
                    "id": rule_id,
                    "ruleKey": cls._required(rule, "ruleKey", "rule_key"),
                    "dayOffsetFromPlanting": offset,
                    "startTimeUtc": start_time,
                    "durationMinutes": duration,
                    "sourceName": str(cls._get(rule, "sourceName", "source_name") or source_name).strip(),
                    "sourceUrl": cls._get(rule, "sourceUrl", "source_url") or source_url,
                    "verifiedAt": verified_at,
                }))
            except (TypeError, ValueError, AttributeError):
                invalid_ids.append(rule_id)
        return irrigation, invalid_ids

    @staticmethod
    def _get(value: dict[str, Any], *keys: str) -> Any:
        for key in keys:
            if key in value:
                return value[key]
        return None

    @classmethod
    def _required(cls, value: dict[str, Any], *keys: str) -> Any:
        result = cls._get(value, *keys)
        if result is None or result == "":
            raise ValueError(f"Verified crop profile is missing {keys[0]}.")
        return result

    @classmethod
    def _required_text(cls, value: dict[str, Any], *keys: str) -> str:
        result = cls._required(value, *keys)
        if not isinstance(result, str) or not result.strip():
            raise ValueError(f"Verified crop profile {keys[0]} must be non-empty text.")
        return result.strip()

    @staticmethod
    def _date(value: Any) -> datetime:
        if isinstance(value, datetime):
            parsed = value
        elif isinstance(value, str):
            parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
        else:
            raise ValueError("Verified crop profile timestamp is invalid.")
        if parsed.tzinfo is None:
            raise ValueError("Verified crop profile timestamp must include a UTC offset.")
        return parsed.astimezone(timezone.utc)
