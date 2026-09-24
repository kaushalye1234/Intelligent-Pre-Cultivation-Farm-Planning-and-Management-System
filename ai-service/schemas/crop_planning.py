from datetime import date, datetime
from typing import Any
from uuid import UUID

from pydantic import Field, field_validator

from schemas.common import AgentEnvelope, CamelModel
from schemas.workflow import WorkflowStep


class CoordinatorInput(CamelModel):
    workflow_id: UUID = Field(alias="workflowId")
    crop_plan_request_id: UUID = Field(alias="cropPlanRequestId")
    farmer_id: UUID = Field(alias="farmerId")
    farm_id: UUID = Field(alias="farmId")
    field_id: UUID | None = Field(default=None, alias="fieldId")
    crop_cycle_id: UUID | None = Field(default=None, alias="cropCycleId")
    crop_type_id: UUID = Field(alias="cropTypeId")
    objective: str
    budget: float
    preferred_start_date: date = Field(alias="preferredStartDate")
    preferred_end_date: date | None = Field(default=None, alias="preferredEndDate")
    crop_variety_id: UUID | None = Field(default=None, alias="cropVarietyId")
    crop_variety_name: str | None = Field(default=None, alias="cropVarietyName")
    cultivation_season: str = Field(default="NotSure", alias="cultivationSeason")
    previous_crop_type_id: UUID | None = Field(default=None, alias="previousCropTypeId")
    previous_crop_type_name: str | None = Field(default=None, alias="previousCropTypeName")
    previous_known_problems: list[str] = Field(default_factory=list, alias="previousKnownProblems")

    @field_validator("objective")
    @classmethod
    def objective_is_bounded(cls, value: str) -> str:
        value = value.strip()
        if not value or len(value) > 500:
            raise ValueError("Objective is required and must be 500 characters or fewer.")
        return value


class CropPlanningCoordinatorOutput(AgentEnvelope):
    reference_data_status: str = Field(default="Unknown", alias="referenceDataStatus")
    objective_summary: str = Field(default="", alias="objectiveSummary")
    steps: list[WorkflowStep] = Field(default_factory=list)


class ToolEnvelope(CamelModel):
    workflow_id: UUID | None = Field(default=None, alias="workflowId")
    tool_name: str = Field(alias="toolName")
    status: str
    data: dict[str, Any] | list[dict[str, Any]] | None = None
    safe_error: str | None = Field(default=None, alias="safeError")


class CropReferenceProfile(CamelModel):
    reference_data_status: str = Field(alias="referenceDataStatus")
    profile: dict[str, Any] | None = None
    stages: list[dict[str, Any]] = Field(default_factory=list)
    rules: list[dict[str, Any]] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)


class CropPlanContext(CamelModel):
    id: UUID
    farm_id: UUID = Field(alias="farmId")
    field_id: UUID | None = Field(default=None, alias="fieldId")
    crop_type_id: UUID = Field(alias="cropTypeId")
    requested_by_user_id: UUID = Field(alias="requestedByUserId")
    preferred_start_date: date = Field(alias="preferredStartDate")
    preferred_end_date: date = Field(alias="preferredEndDate")
    budget: float
    objective: str
    status: str
    farm: dict[str, Any]
    field: dict[str, Any] | None = None
    crop_type: dict[str, Any] = Field(alias="cropType")
    crop_variety_id: UUID | None = Field(default=None, alias="cropVarietyId")
    crop_variety_name: str | None = Field(default=None, alias="cropVarietyName")
    cultivation_season: str = Field(default="NotSure", alias="cultivationSeason")
    previous_crop_type_id: UUID | None = Field(default=None, alias="previousCropTypeId")
    previous_crop_type_name: str | None = Field(default=None, alias="previousCropTypeName")
    previous_known_problems: list[str] = Field(default_factory=list, alias="previousKnownProblems")


class ToolCallRecord(CamelModel):
    tool_name: str = Field(alias="toolName")
    status: str
    safe_error: str | None = Field(default=None, alias="safeError")
    captured_at: datetime = Field(default_factory=datetime.utcnow, alias="capturedAt")
