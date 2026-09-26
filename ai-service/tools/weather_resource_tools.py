from typing import Any
from uuid import UUID

from schemas.weather_resource import CropResourceRequirements, ReservationSnapshot, StockSnapshot, WeatherForecast
from tools.backend_tool_client import BackendToolClient

GET_CROP_RESOURCE_REQUIREMENTS = "GetCropResourceRequirements"
GET_FIELD_DETAILS = "GetFieldDetails"
GET_RESOURCE_AVAILABILITY = "GetResourceAvailability"
GET_EXISTING_RESERVATIONS = "GetExistingReservations"
GET_LOW_STOCK_STATUS = "GetLowStockStatus"
GET_WEATHER_FORECAST = "GetWeatherForecast"


class WeatherResourceTools:
    """Read-only Member 3 tools. Every call carries the workflow and step so the backend scopes and logs it."""

    def __init__(self, client: BackendToolClient) -> None:
        self._client = client

    async def get_crop_resource_requirements(
        self, crop_plan_request_id: UUID, workflow_id: UUID, agent_step_id: UUID
    ) -> CropResourceRequirements:
        data = await self._get(f"/api/internal/agent-tools/crop-resource-requirements/{crop_plan_request_id}", workflow_id, agent_step_id)
        return CropResourceRequirements.model_validate(data)

    async def get_field_details(self, field_id: UUID, workflow_id: UUID, agent_step_id: UUID) -> dict[str, Any]:
        return dict(await self._get(f"/api/internal/agent-tools/fields/{field_id}", workflow_id, agent_step_id) or {})

    async def get_resource_availability(
        self, resource_ids: list[UUID], workflow_id: UUID, agent_step_id: UUID
    ) -> list[StockSnapshot]:
        data = await self._get("/api/internal/agent-tools/resource-availability", workflow_id, agent_step_id, resource_ids)
        return [StockSnapshot.model_validate(item) for item in list(data or [])]

    async def get_existing_reservations(
        self, resource_ids: list[UUID], workflow_id: UUID, agent_step_id: UUID
    ) -> list[ReservationSnapshot]:
        data = await self._get("/api/internal/agent-tools/existing-reservations", workflow_id, agent_step_id, resource_ids)
        return [ReservationSnapshot.model_validate(item) for item in list(data or [])]

    async def get_low_stock_status(self, workflow_id: UUID, agent_step_id: UUID) -> list[StockSnapshot]:
        data = await self._get("/api/internal/agent-tools/low-stock-status", workflow_id, agent_step_id)
        return [StockSnapshot.model_validate(item) for item in list(data or [])]

    async def get_weather_forecast(self, workflow_id: UUID, agent_step_id: UUID) -> WeatherForecast:
        data = await self._get("/api/internal/agent-tools/weather-forecast", workflow_id, agent_step_id)
        return WeatherForecast.model_validate(data)

    async def _get(
        self,
        path: str,
        workflow_id: UUID,
        agent_step_id: UUID,
        resource_ids: list[UUID] | None = None,
    ) -> Any:
        params: dict[str, Any] = {"agentStepId": str(agent_step_id)}
        if resource_ids:
            params["resourceIds"] = [str(resource_id) for resource_id in resource_ids]
        envelope = await self._client.get(path, workflow_id=workflow_id, params=params)
        return envelope.data
