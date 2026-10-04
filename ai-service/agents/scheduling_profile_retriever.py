import asyncio
from datetime import datetime, timezone
from typing import Any

from providers.base_llm_provider import BaseLLMProvider
from schemas.scheduling_validation import SchedulingValidationInput
from tools.scheduling_evidence_tools import SchedulingEvidenceTools

SCHEDULING_PROFILE_TOOL: dict[str, Any] = {
    "name": "GetVerifiedCropProfile",
    "description": "Retrieve the persisted verified crop profile for the current workflow.",
    "parameters": {"type": "object", "properties": {}, "required": [], "additionalProperties": False},
    "strict": True,
}
OVERALL_TIMEOUT_SECONDS = 45


class SchedulingProfileRetriever:
    def __init__(self, provider: BaseLLMProvider, tools: SchedulingEvidenceTools) -> None:
        self._provider = provider
        self._tools = tools

    async def retrieve_profile(self, request: SchedulingValidationInput) -> SchedulingValidationInput:
        if not self._eligible(request):
            return request
        try:
            return await asyncio.wait_for(self._retrieve_once(request), timeout=OVERALL_TIMEOUT_SECONDS)
        except Exception:
            return request

    async def _retrieve_once(self, request: SchedulingValidationInput) -> SchedulingValidationInput:
        result = await self._provider.generate_tool_call(
            "If the scheduling request needs verified crop-profile evidence, call GetVerifiedCropProfile once. "
            "Do not provide crop, workflow, profile, or other identifiers. Do not provide scheduling advice.",
            SCHEDULING_PROFILE_TOOL,
        )
        call = result.function_call
        if call is None or call.name != "GetVerifiedCropProfile" or call.arguments != {}:
            return request
        evidence = await self._tools.get_verified_crop_profile(request)
        expected_profile = self._member3_profile_id(request)
        if expected_profile and str(evidence.profile_id).casefold() != expected_profile.casefold():
            return request
        if not evidence.profile_id or not evidence.stages or not evidence.source_name or not evidence.source_version:
            return request
        if evidence.verified_at is None or evidence.verified_at.astimezone(timezone.utc) > datetime.now(timezone.utc):
            return request
        original = request.evidence
        if original is None:
            return request
        updated = original.model_copy(update={
            "profile_id": evidence.profile_id,
            "source_name": evidence.source_name,
            "source_url": evidence.source_url,
            "source_version": evidence.source_version,
            "verified_at": evidence.verified_at,
            "stages": evidence.stages,
        })
        return request.model_copy(update={"evidence": updated})

    @staticmethod
    def _eligible(request: SchedulingValidationInput) -> bool:
        evidence = request.evidence
        if evidence is None or (evidence.profile_id and evidence.stages):
            return False
        if not all((evidence.coordinator_step_id, evidence.field_analysis_step_id, evidence.weather_resource_step_id)):
            return False
        for output, expected in (
            (request.coordinator_output, "Planned"),
            (request.field_analysis_output, "Analyzed"),
            (request.weather_resource_output, "Analyzed"),
        ):
            if output.get("status") != expected or str(output.get("workflowId", "")).casefold() != str(request.workflow_id).casefold():
                return False
        return True

    @staticmethod
    def _member3_profile_id(request: SchedulingValidationInput) -> str | None:
        requirement_source = request.weather_resource_output.get("requirementSource")
        if not isinstance(requirement_source, dict):
            return None
        profile_id = requirement_source.get("cropReferenceProfileId")
        return str(profile_id) if profile_id else None
