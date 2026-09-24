from datetime import date
from uuid import UUID

from pydantic import Field, field_validator

from schemas.common import AgentEnvelope, CamelModel


class WeatherDay(CamelModel):
    date: date
    min_temperature_c: float = Field(alias="minTemperatureC")
    max_temperature_c: float = Field(alias="maxTemperatureC")
    rain_mm: float = Field(alias="rainMm")
    max_wind_speed_ms: float = Field(alias="maxWindSpeedMs")
    description: str = ""


class WeatherForecast(CamelModel):
    location: str
    is_available: bool = Field(alias="isAvailable")
    message: str = ""
    days: list[WeatherDay] = Field(default_factory=list)


class StockSnapshot(CamelModel):
    inventory_stock_id: UUID = Field(alias="inventoryStockId")
    resource_id: UUID = Field(alias="resourceId")
    resource_name: str = Field(alias="resourceName")
    unit: str
    quantity_on_hand: float = Field(alias="quantityOnHand")
    reserved_quantity: float = Field(alias="reservedQuantity")
    available_quantity: float = Field(alias="availableQuantity")
    low_stock_threshold: float = Field(alias="lowStockThreshold")


class ResourceRequirement(CamelModel):
    """How much of one resource crop planning says is needed. Never estimated by the agent."""

    resource_id: UUID = Field(alias="resourceId")
    requested_quantity: float = Field(alias="requestedQuantity", ge=0)


class WeatherResourceInput(CamelModel):
    workflow_id: UUID = Field(alias="workflowId")
    agent_step_id: UUID = Field(alias="agentStepId")
    crop_plan_request_id: UUID = Field(alias="cropPlanRequestId")
    location: str
    preferred_start_date: date = Field(alias="preferredStartDate")
    preferred_end_date: date = Field(alias="preferredEndDate")
    field_priority: str = Field(alias="fieldPriority")
    field_analysis_summary: str = Field(alias="fieldAnalysisSummary")
    weather: WeatherForecast
    stocks: list[StockSnapshot] = Field(default_factory=list)
    resource_requirements: list[ResourceRequirement] = Field(default_factory=list, alias="resourceRequirements")

    @field_validator("location")
    @classmethod
    def location_is_bounded(cls, value: str) -> str:
        value = value.strip()
        if len(value) > 200:
            raise ValueError("Location must be 200 characters or fewer.")
        return value

    @field_validator("stocks")
    @classmethod
    def stocks_are_bounded(cls, value: list[StockSnapshot]) -> list[StockSnapshot]:
        if len(value) > 100:
            raise ValueError("At most 100 inventory rows may be analyzed.")
        return value


class ResourceCheck(CamelModel):
    inventory_stock_id: UUID = Field(alias="inventoryStockId")
    resource_id: UUID = Field(alias="resourceId")
    resource_name: str = Field(alias="resourceName")
    unit: str
    available_quantity: float = Field(alias="availableQuantity")
    is_low_stock: bool = Field(alias="isLowStock")
    # requested/sufficient stay None (status ResourceRequirementUnknown) when no requirement was supplied.
    requested: float | None = None
    sufficient: bool | None = None
    requirement_status: str = Field(default="ResourceRequirementUnknown", alias="requirementStatus")


class WeatherResourceOutput(AgentEnvelope):
    weather_risk: str = Field(alias="weatherRisk")
    weather_summary: str = Field(alias="weatherSummary")
    resource_checks: list[ResourceCheck] = Field(default_factory=list, alias="resourceChecks")
    recommendations: list[str] = Field(default_factory=list)
