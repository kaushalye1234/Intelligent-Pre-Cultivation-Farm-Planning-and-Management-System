from datetime import date, datetime
from typing import Any
from uuid import UUID

from pydantic import Field, field_validator

from schemas.common import AgentEnvelope, CamelModel


class ExistingFarmTask(CamelModel):
    id: UUID
    assigned_to_user_id: UUID = Field(alias="assignedToUserId")
    due_at: datetime = Field(alias="dueAt")
    status: int


class ExistingIrrigation(CamelModel):
    id: UUID
    field_id: UUID = Field(alias="fieldId")
    scheduled_at: datetime = Field(alias="scheduledAt")
    duration_minutes: int = Field(alias="durationMinutes")
    status: int


class SchedulingValidationInput(CamelModel):
    workflow_id: UUID = Field(alias="workflowId")
    candidate_revision: int = Field(alias="candidateRevision", ge=1)
    crop_plan_request_id: UUID = Field(alias="cropPlanRequestId")
    farm_id: UUID = Field(alias="farmId")
    field_id: UUID | None = Field(default=None, alias="fieldId")
    assigned_to_user_id: UUID = Field(alias="assignedToUserId")
    preferred_start_date: date = Field(alias="preferredStartDate")
    preferred_end_date: date = Field(alias="preferredEndDate")
    budget: float = Field(ge=0)
    coordinator_output: dict[str, Any] = Field(alias="coordinatorOutput")
    field_analysis_output: dict[str, Any] = Field(alias="fieldAnalysisOutput")
    weather_resource_output: dict[str, Any] = Field(alias="weatherResourceOutput")
    existing_tasks: list[ExistingFarmTask] = Field(default_factory=list, alias="existingTasks")
    existing_irrigation: list[ExistingIrrigation] = Field(default_factory=list, alias="existingIrrigation")

    @field_validator("existing_tasks")
    @classmethod
    def tasks_are_bounded(cls, value: list[ExistingFarmTask]) -> list[ExistingFarmTask]:
        if len(value) > 500:
            raise ValueError("At most 500 existing tasks may be supplied.")
        return value

    @field_validator("existing_irrigation")
    @classmethod
    def irrigation_is_bounded(cls, value: list[ExistingIrrigation]) -> list[ExistingIrrigation]:
        if len(value) > 500:
            raise ValueError("At most 500 existing irrigation schedules may be supplied.")
        return value


class CandidateTask(CamelModel):
    farm_id: UUID = Field(alias="farmId")
    title: str
    description: str
    due_at: datetime = Field(alias="dueAt")
    assigned_to_user_id: UUID = Field(alias="assignedToUserId")


class CandidateIrrigation(CamelModel):
    field_id: UUID = Field(alias="fieldId")
    scheduled_at: datetime = Field(alias="scheduledAt")
    duration_minutes: int = Field(alias="durationMinutes", ge=1, le=1440)
    notes: str


class CandidateReservation(CamelModel):
    inventory_stock_id: UUID = Field(alias="inventoryStockId")
    quantity: float = Field(gt=0)
    purpose: str
    estimated_unit_cost: float | None = Field(default=None, alias="estimatedUnitCost", ge=0)


class SchedulingConstraint(CamelModel):
    code: str
    severity: str
    message: str


class SchedulingValidationOutput(AgentEnvelope):
    candidate_revision: int = Field(alias="candidateRevision")
    requires_human_approval: bool = Field(alias="requiresHumanApproval")
    candidate_tasks: list[CandidateTask] = Field(default_factory=list, alias="candidateTasks")
    candidate_irrigation: list[CandidateIrrigation] = Field(default_factory=list, alias="candidateIrrigation")
    candidate_reservations: list[CandidateReservation] = Field(default_factory=list, alias="candidateReservations")
    estimated_cost: float | None = Field(default=None, alias="estimatedCost")
    constraints: list[SchedulingConstraint] = Field(default_factory=list)
