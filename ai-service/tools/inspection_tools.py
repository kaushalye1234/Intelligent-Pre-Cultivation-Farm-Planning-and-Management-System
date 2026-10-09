from typing import Any
from uuid import UUID

from schemas.field_analysis import InspectionEvidence
from tools.backend_tool_client import BackendToolClient


class InspectionTools:
    def __init__(self, client: BackendToolClient) -> None:
        self._client = client

    async def get_crop_plan_context(
        self,
        crop_plan_request_id: UUID,
        workflow_id: UUID,
        agent_step_id: UUID | None = None,
    ) -> dict[str, Any]:
        return await self._get_dict(
            f"/api/internal/agent-tools/crop-plan-context/{crop_plan_request_id}",
            workflow_id,
            agent_step_id,
        )

    async def get_field_details(self, field_id: UUID, workflow_id: UUID, agent_step_id: UUID | None = None) -> dict[str, Any]:
        return await self._get_dict(f"/api/internal/agent-tools/fields/{field_id}", workflow_id, agent_step_id)

    async def get_crop_cycle_details(self, crop_cycle_id: UUID, workflow_id: UUID, agent_step_id: UUID | None = None) -> dict[str, Any]:
        return await self._get_dict(f"/api/internal/agent-tools/crop-cycles/{crop_cycle_id}", workflow_id, agent_step_id)

    async def get_recent_inspections(
        self,
        field_id: UUID,
        crop_plan_request_id: UUID,
        pre_planting_inspection_id: UUID,
        workflow_id: UUID,
        agent_step_id: UUID | None = None,
    ) -> list[InspectionEvidence]:
        envelope = await self._client.get(
            "/api/internal/agent-tools/recent-inspections",
            workflow_id=workflow_id,
            params=self._params(agent_step_id, {
                "fieldId": str(field_id),
                "cropPlanRequestId": str(crop_plan_request_id),
                "prePlantingInspectionId": str(pre_planting_inspection_id),
            }),
        )
        return [InspectionEvidence.model_validate(item) for item in list(envelope.data or [])]

    async def get_open_crop_issues(
        self,
        field_id: UUID,
        crop_plan_request_id: UUID,
        pre_planting_inspection_id: UUID,
        workflow_id: UUID,
        agent_step_id: UUID | None = None,
    ) -> list[dict[str, Any]]:
        envelope = await self._client.get(
            "/api/internal/agent-tools/open-crop-issues",
            workflow_id=workflow_id,
            params=self._params(agent_step_id, {
                "fieldId": str(field_id),
                "cropPlanRequestId": str(crop_plan_request_id),
                "prePlantingInspectionId": str(pre_planting_inspection_id),
            }),
        )
        return list(envelope.data or [])

    async def get_inspection_image_metadata(
        self,
        field_id: UUID,
        crop_plan_request_id: UUID,
        pre_planting_inspection_id: UUID,
        workflow_id: UUID,
        agent_step_id: UUID | None = None,
    ) -> list[dict[str, Any]]:
        envelope = await self._client.get(
            "/api/internal/agent-tools/inspection-image-metadata",
            workflow_id=workflow_id,
            params=self._params(agent_step_id, {
                "fieldId": str(field_id),
                "cropPlanRequestId": str(crop_plan_request_id),
                "prePlantingInspectionId": str(pre_planting_inspection_id),
            }),
        )
        return list(envelope.data or [])

    async def get_crop_reference_profile(
        self,
        workflow_id: UUID,
        crop_reference_profile_id: UUID | None,
        agent_step_id: UUID | None = None,
    ) -> dict[str, Any]:
        params = self._params(agent_step_id)
        if crop_reference_profile_id:
            params["cropReferenceProfileId"] = str(crop_reference_profile_id)
        envelope = await self._client.get("/api/internal/agent-tools/crop-reference-profiles", workflow_id=workflow_id, params=params)
        return dict(envelope.data or {})

    async def _get_dict(self, path: str, workflow_id: UUID, agent_step_id: UUID | None) -> dict[str, Any]:
        envelope = await self._client.get(path, workflow_id=workflow_id, params=self._params(agent_step_id))
        return dict(envelope.data or {})

    @staticmethod
    def _params(agent_step_id: UUID | None, values: dict[str, Any] | None = None) -> dict[str, Any]:
        params = dict(values or {})
        if agent_step_id:
            params["agentStepId"] = str(agent_step_id)
        return params
