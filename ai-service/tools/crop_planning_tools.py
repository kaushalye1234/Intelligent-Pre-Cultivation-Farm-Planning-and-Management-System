from typing import Any
from uuid import UUID

from schemas.crop_planning import CropPlanContext, CropReferenceProfile
from tools.backend_tool_client import BackendToolClient


class CropPlanningTools:
    def __init__(self, client: BackendToolClient) -> None:
        self._client = client

    async def get_crop_plan_context(self, crop_plan_request_id: UUID, workflow_id: UUID) -> CropPlanContext:
        envelope = await self._client.get(f"/api/internal/agent-tools/crop-plan-context/{crop_plan_request_id}", workflow_id=workflow_id)
        return CropPlanContext.model_validate(envelope.data)

    async def get_farm_details(self, farm_id: UUID, workflow_id: UUID) -> dict[str, Any]:
        envelope = await self._client.get(f"/api/internal/agent-tools/farms/{farm_id}", workflow_id=workflow_id)
        return dict(envelope.data or {})

    async def get_field_details(self, field_id: UUID, workflow_id: UUID) -> dict[str, Any]:
        envelope = await self._client.get(f"/api/internal/agent-tools/fields/{field_id}", workflow_id=workflow_id)
        return dict(envelope.data or {})

    async def get_crop_cycle_details(self, crop_cycle_id: UUID, workflow_id: UUID) -> dict[str, Any]:
        envelope = await self._client.get(f"/api/internal/agent-tools/crop-cycles/{crop_cycle_id}", workflow_id=workflow_id)
        return dict(envelope.data or {})

    async def get_crop_reference_profile(self, crop_type_id: UUID, workflow_id: UUID) -> CropReferenceProfile:
        envelope = await self._client.get(
            "/api/internal/agent-tools/crop-reference-profiles",
            workflow_id=workflow_id,
            params={"cropTypeId": str(crop_type_id)},
        )
        return CropReferenceProfile.model_validate(envelope.data)

    async def get_recent_crop_plan_history(self, crop_plan_request_id: UUID, workflow_id: UUID) -> list[dict[str, Any]]:
        envelope = await self._client.get(f"/api/internal/agent-tools/crop-plan-history/{crop_plan_request_id}", workflow_id=workflow_id)
        return list(envelope.data or [])
