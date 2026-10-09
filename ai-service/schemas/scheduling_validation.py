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


class SchedulingStageEvidence(CamelModel):
    id: UUID
    stage_name: str = Field(alias="stageName")
    sequence: int
    typical_min_days: int | None = Field(default=None, alias="typicalMinDays")
    typical_max_days: int | None = Field(default=None, alias="typicalMaxDays")
    source_name: str = Field(alias="sourceName")
    source_url: str | None = Field(default=None, alias="sourceUrl")


class SchedulingIrrigationRuleEvidence(CamelModel):
    id: UUID
    rule_key: str = Field(alias="ruleKey")
    day_offset_from_planting: int = Field(alias="dayOffsetFromPlanting", ge=0, le=365)
    start_time_utc: str = Field(alias="startTimeUtc")
    duration_minutes: int = Field(alias="durationMinutes", ge=1, le=1440)
    source_name: str = Field(alias="sourceName")
    source_url: str | None = Field(default=None, alias="sourceUrl")
    verified_at: datetime = Field(alias="verifiedAt")


class SchedulingEvidenceBundle(CamelModel):
    profile_id: UUID | None = Field(default=None, alias="profileId")
    source_name: str | None = Field(default=None, alias="sourceName")
    source_url: str | None = Field(default=None, alias="sourceUrl")
    source_version: str | None = Field(default=None, alias="sourceVersion")
    verified_at: datetime | None = Field(default=None, alias="verifiedAt")
    coordinator_step_id: UUID | None = Field(default=None, alias="coordinatorStepId")
    field_analysis_step_id: UUID | None = Field(default=None, alias="fieldAnalysisStepId")
    weather_resource_step_id: UUID | None = Field(default=None, alias="weatherResourceStepId")
    stages: list[SchedulingStageEvidence] = Field(default_factory=list)
    irrigation_rules: list[SchedulingIrrigationRuleEvidence] = Field(default_factory=list, alias="irrigationRules")
    invalid_irrigation_rule_ids: list[UUID] = Field(default_factory=list, alias="invalidIrrigationRuleIds")


class SchedulingSource(CamelModel):
    kind: str
    id: UUID
    label: str = Field(min_length=1, max_length=180)
    profile_id: UUID | None = Field(default=None, alias="profileId")
    source_version: str | None = Field(default=None, alias="sourceVersion")
    verified_at: datetime | None = Field(default=None, alias="verifiedAt")
    source_url: str | None = Field(default=None, alias="sourceUrl")


class SchedulingValidationInput(CamelModel):
    contract_version: int = Field(default=2, alias="contractVersion")
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
    evidence: SchedulingEvidenceBundle | None = None
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
    reason: str = Field(default="", max_length=500)
    sources: list[SchedulingSource] = Field(default_factory=list, max_length=4)


class CandidateIrrigation(CamelModel):
    field_id: UUID = Field(alias="fieldId")
    scheduled_at: datetime = Field(alias="scheduledAt")
    duration_minutes: int = Field(alias="durationMinutes", ge=1, le=1440)
    notes: str
    reason: str = Field(default="", max_length=500)
    sources: list[SchedulingSource] = Field(default_factory=list, max_length=4)


class CandidateReservation(CamelModel):
    inventory_stock_id: UUID = Field(alias="inventoryStockId")
    quantity: float = Field(gt=0)
    purpose: str
    estimated_unit_cost: float | None = Field(default=None, alias="estimatedUnitCost", ge=0)
    reason: str = Field(default="", max_length=500)
    sources: list[SchedulingSource] = Field(default_factory=list, max_length=4)


class SchedulingConstraint(CamelModel):
    code: str
    severity: str
    message: str


class SchedulingValidationOutput(AgentEnvelope):
    contract_version: int = Field(default=2, alias="contractVersion")
    candidate_revision: int = Field(alias="candidateRevision")
    requires_human_approval: bool = Field(alias="requiresHumanApproval")
    candidate_tasks: list[CandidateTask] = Field(default_factory=list, alias="candidateTasks")
    candidate_irrigation: list[CandidateIrrigation] = Field(default_factory=list, alias="candidateIrrigation")
    candidate_reservations: list[CandidateReservation] = Field(default_factory=list, alias="candidateReservations")
    estimated_cost: float | None = Field(default=None, alias="estimatedCost")
    constraints: list[SchedulingConstraint] = Field(default_factory=list)
