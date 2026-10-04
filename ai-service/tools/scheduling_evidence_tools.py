import json
from datetime import datetime, timezone
from typing import Any

from schemas.crop_planning import CropPlanContext, CropReferenceProfile
from schemas.scheduling_validation import SchedulingEvidenceBundle
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
        crop_type_id = cls._required(profile, "cropTypeId", "crop_type_id")
        if str(crop_type_id).casefold() != str(context.crop_type_id).casefold():
            raise ValueError("Verified crop profile does not match the persisted crop type.")
        variety = cls._get(profile, "varietyName", "variety_name")
        if (variety or None) != (context.crop_variety_name or None):
            raise ValueError("Verified crop profile does not match the persisted crop variety.")
        if not cls._get(profile, "isActive", "is_active"):
            raise ValueError("Verified crop profile is inactive.")
        source_name = cls._required(profile, "sourceName", "source_name")
        source_version = cls._required(profile, "sourceVersion", "source_version")
        verified_at = cls._date(cls._required(profile, "verifiedAt", "verified_at"))
        if verified_at > datetime.now(timezone.utc):
            raise ValueError("Verified crop profile has a future verification time.")

        stages = []
        for stage in reference.stages:
            stages.append({
                "id": cls._required(stage, "id"),
                "stageName": cls._required(stage, "stageName", "stage_name"),
                "sequence": int(cls._required(stage, "sequence")),
                "typicalMinDays": cls._get(stage, "typicalMinDays", "typical_min_days"),
                "typicalMaxDays": cls._get(stage, "typicalMaxDays", "typical_max_days"),
                "sourceName": cls._get(stage, "sourceName", "source_name") or source_name,
                "sourceUrl": cls._get(stage, "sourceUrl", "source_url"),
            })
        if not stages:
            raise ValueError("Verified crop profile contains no verified stages.")

        original = request.evidence
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
            irrigationRules=original.irrigation_rules if original else [],
            invalidIrrigationRuleIds=original.invalid_irrigation_rule_ids if original else [],
        )

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
