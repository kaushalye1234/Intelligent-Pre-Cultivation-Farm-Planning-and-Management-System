from datetime import date, datetime
from typing import Literal
from uuid import UUID

from pydantic import Field, field_validator

from schemas.common import AgentEnvelope, CamelModel

MEMBER3_CROP_HEALTH_CONSIDERATION_CONTRACT_VERSION = 1

SUFFICIENT = "Sufficient"
INSUFFICIENT = "Insufficient"
REQUIREMENT_UNKNOWN = "ResourceRequirementUnknown"
NOT_COMPARABLE = "InventoryNotComparable"
INCOMPLETE = "Incomplete"


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
    """One inventory row from GetResourceAvailability / GetLowStockStatus. availableQuantity already nets reservations."""

    inventory_stock_id: UUID = Field(alias="inventoryStockId")
    resource_id: UUID = Field(alias="resourceId")
    resource_name: str = Field(alias="resourceName")
    unit: str
    quantity_on_hand: float = Field(alias="quantityOnHand")
    reserved_quantity: float = Field(alias="reservedQuantity")
    available_quantity: float = Field(alias="availableQuantity")
    low_stock_threshold: float = Field(alias="lowStockThreshold")


class ReservationSnapshot(CamelModel):
    reservation_id: UUID = Field(alias="reservationId")
    inventory_stock_id: UUID = Field(alias="inventoryStockId")
    resource_id: UUID = Field(alias="resourceId")
    resource_name: str = Field(alias="resourceName")
    unit: str
    quantity: float
    purpose: str = ""
    created_at: datetime = Field(alias="createdAt")


class RequirementSource(CamelModel):
    crop_reference_profile_id: UUID = Field(alias="cropReferenceProfileId")
    source_name: str = Field(alias="sourceName")
    source_url: str | None = Field(default=None, alias="sourceUrl")
    source_version: str = Field(alias="sourceVersion")
    verified_at: datetime = Field(alias="verifiedAt")
    region: str | None = None
    variety_name: str | None = Field(default=None, alias="varietyName")


class CalculatedResourceRequirement(CamelModel):
    """One verified requirement rule. requiredQuantity is calculated by the backend, or None when unknown."""

    rule_id: UUID = Field(alias="ruleId")
    rule_key: str = Field(alias="ruleKey")
    resource_id: UUID | None = Field(default=None, alias="resourceId")
    resource_name: str = Field(alias="resourceName")
    resource_match: str = Field(alias="resourceMatch")
    quantity_per_area: float | None = Field(default=None, alias="quantityPerArea")
    resource_unit: str | None = Field(default=None, alias="resourceUnit")
    area_unit: str | None = Field(default=None, alias="areaUnit")
    required_quantity: float | None = Field(default=None, alias="requiredQuantity")
    status: str
    basis: str | None = None
    reason: str | None = None


class CropResourceRequirements(CamelModel):
    """GetCropResourceRequirements result. status is Available, Incomplete or Unavailable."""

    crop_plan_request_id: UUID = Field(alias="cropPlanRequestId")
    crop_type_id: UUID = Field(alias="cropTypeId")
    crop_name: str = Field(alias="cropName")
    variety_name: str | None = Field(default=None, alias="varietyName")
    field_id: UUID | None = Field(default=None, alias="fieldId")
    field_area: float | None = Field(default=None, alias="fieldArea")
    field_area_unit: str | None = Field(default=None, alias="fieldAreaUnit")
    status: str
    reason: str | None = None
    source: RequirementSource | None = None
    requirements: list[CalculatedResourceRequirement] = Field(default_factory=list)


class Member2FieldAnalysisContext(CamelModel):
    """Completed safe Member 2 output; never raw inspection evidence or staff notes."""

    field_suitability: str = Field(alias="fieldSuitability")
    soil_assessment: str = Field(alias="soilAssessment")
    water_assessment: str = Field(alias="waterAssessment")
    drainage_assessment: str = Field(alias="drainageAssessment")
    field_preparation_requirements: list[str] = Field(default_factory=list, alias="fieldPreparationRequirements")
    planting_readiness: str = Field(alias="plantingReadiness")
    identified_risks: list[str] = Field(default_factory=list, alias="identifiedRisks")
    recommended_pre_planting_actions: list[str] = Field(default_factory=list, alias="recommendedPrePlantingActions")
    priority: str
    warnings: list[str] = Field(default_factory=list)
    requires_human_review: bool = Field(alias="requiresHumanReview")
    reviewed_crop_issue_actions: list["Member2CropHealthActionContext"] = Field(
        default_factory=list, alias="reviewedCropIssueActions", max_length=20
    )


class Member2CropHealthActionContext(CamelModel):
    action_key: str = Field(alias="actionKey", min_length=1, max_length=80)
    action_type: str = Field(alias="actionType", min_length=1, max_length=80)
    order: int = Field(ge=0, le=20)
    timing_category: str = Field(alias="timingCategory", min_length=1, max_length=80)


class WeatherResourceInput(CamelModel):
    """Crop plan context from ASP.NET. Evidence is gathered by the agent through the backend tools."""

    workflow_id: UUID = Field(alias="workflowId")
    agent_step_id: UUID = Field(alias="agentStepId")
    crop_plan_request_id: UUID = Field(alias="cropPlanRequestId")
    field_id: UUID | None = Field(default=None, alias="fieldId")
    location: str
    preferred_start_date: date = Field(alias="preferredStartDate")
    preferred_end_date: date = Field(alias="preferredEndDate")
    field_priority: str = Field(alias="fieldPriority")
    field_analysis_summary: str = Field(alias="fieldAnalysisSummary")
    member_2_field_analysis_context: Member2FieldAnalysisContext | None = Field(
        default=None,
        alias="member2FieldAnalysisContext",
    )

    @field_validator("location")
    @classmethod
    def location_is_bounded(cls, value: str) -> str:
        value = value.strip()
        if len(value) > 200:
            raise ValueError("Location must be 200 characters or fewer.")
        return value


class ResourceCheck(CamelModel):
    inventory_stock_id: UUID = Field(alias="inventoryStockId")
    resource_id: UUID = Field(alias="resourceId")
    resource_name: str = Field(alias="resourceName")
    unit: str
    available_quantity: float = Field(alias="availableQuantity")
    is_low_stock: bool = Field(alias="isLowStock")
    # requested/sufficient stay None (status ResourceRequirementUnknown) when no verified requirement exists.
    requested: float | None = None
    sufficient: bool | None = None
    requirement_status: str = Field(default=REQUIREMENT_UNKNOWN, alias="requirementStatus")


class ResourceRequirementAssessment(CamelModel):
    """The agent's assessment of one verified requirement against inventory after reservations."""

    rule_id: UUID | None = Field(default=None, alias="ruleId")
    resource_id: UUID | None = Field(default=None, alias="resourceId")
    resource_name: str = Field(alias="resourceName")
    unit: str | None = None
    required_quantity: float | None = Field(default=None, alias="requiredQuantity")
    available_quantity: float | None = Field(default=None, alias="availableQuantity")
    reserved_quantity: float | None = Field(default=None, alias="reservedQuantity")
    shortage_quantity: float | None = Field(default=None, alias="shortageQuantity")
    sufficient: bool | None = None
    requirement_status: str = Field(alias="requirementStatus")
    basis: str | None = None
    reason: str | None = None


class CropHealthWeatherResourceConsideration(CamelModel):
    contract_version: Literal[1] = Field(
        default=MEMBER3_CROP_HEALTH_CONSIDERATION_CONTRACT_VERSION,
        alias="contractVersion",
    )
    action_key: str = Field(alias="actionKey", min_length=1, max_length=80)
    consideration_type: str = Field(alias="considerationType", min_length=1, max_length=80)
    note: str = Field(min_length=1, max_length=500)


class WeatherResourceOutput(AgentEnvelope):
    weather_risk: str = Field(alias="weatherRisk")
    weather_summary: str = Field(alias="weatherSummary")
    resource_checks: list[ResourceCheck] = Field(default_factory=list, alias="resourceChecks")
    recommendations: list[str] = Field(default_factory=list)
    resource_requirements: list[ResourceRequirementAssessment] = Field(default_factory=list, alias="resourceRequirements")
    requirement_status: str = Field(default=REQUIREMENT_UNKNOWN, alias="requirementStatus")
    requirement_source: RequirementSource | None = Field(default=None, alias="requirementSource")
    reason: str | None = None
    tools_used: list[str] = Field(default_factory=list, alias="toolsUsed")
    crop_health_considerations: list[CropHealthWeatherResourceConsideration] = Field(
        default_factory=list, alias="cropHealthConsiderations", max_length=20
    )
