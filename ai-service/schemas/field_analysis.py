from datetime import datetime
from typing import Any, Literal
from uuid import UUID

from pydantic import Field, field_validator

from schemas.common import AgentEnvelope, CamelModel


class FieldAnalysisInput(CamelModel):
    workflow_id: UUID = Field(alias="workflowId")
    crop_plan_request_id: UUID = Field(alias="cropPlanRequestId")
    pre_planting_inspection_id: UUID = Field(alias="prePlantingInspectionId")
    field_id: UUID = Field(alias="fieldId")
    crop_cycle_id: UUID | None = Field(default=None, alias="cropCycleId")
    requested_analysis: list[str] = Field(default_factory=list, alias="requestedAnalysis")
    crop_reference_profile_id: UUID | None = Field(default=None, alias="cropReferenceProfileId")
    agent_step_id: UUID | None = Field(default=None, alias="agentStepId")

    @field_validator("requested_analysis")
    @classmethod
    def requested_analysis_is_bounded(cls, value: list[str]) -> list[str]:
        cleaned = [item.strip() for item in value if item.strip()]
        if len(cleaned) > 12:
            raise ValueError("Requested analysis must contain 12 items or fewer.")
        return cleaned


class FieldCondition(CamelModel):
    summary: str = ""
    evidence_inspection_ids: list[UUID] = Field(default_factory=list, alias="evidenceInspectionIds")


class OpenIssueSummary(CamelModel):
    issue_id: UUID = Field(alias="issueId")
    severity: str
    status: str
    evidence_inspection_id: UUID | None = Field(default=None, alias="evidenceInspectionId")


class CropFieldAnalysisOutput(AgentEnvelope):
    field_condition: FieldCondition = Field(default_factory=FieldCondition, alias="fieldCondition")
    open_issues: list[OpenIssueSummary] = Field(default_factory=list, alias="openIssues")
    priority: Literal["High", "Medium", "Low", "Unknown"]
    field_suitability: Literal[
        "Suitable", "SuitableWithConditions", "NotSuitable", "RequiresFurtherAssessment", "Unknown"
    ] = Field(alias="fieldSuitability")
    soil_assessment: str = Field(alias="soilAssessment", max_length=2000)
    water_assessment: str = Field(alias="waterAssessment", max_length=2000)
    drainage_assessment: str = Field(alias="drainageAssessment", max_length=2000)
    field_preparation_requirements: list[str] = Field(alias="fieldPreparationRequirements", max_length=20)
    planting_readiness: Literal[
        "Ready", "ReadyWithMinorPreparation", "RequiresPreparation", "NotReady",
        "RequiresFurtherAssessment", "Unknown",
    ] = Field(alias="plantingReadiness")
    identified_risks: list[Literal[
        "WaterShortageRisk", "FloodingRisk", "PoorDrainage", "SoilSuitabilityConcern",
        "SoilErosion", "FieldAccessProblem", "LandPreparationRequired", "Other",
    ]] = Field(alias="identifiedRisks", max_length=20)
    recommended_pre_planting_actions: list[str] = Field(alias="recommendedPrePlantingActions", max_length=20)

    @field_validator("field_preparation_requirements", "recommended_pre_planting_actions")
    @classmethod
    def action_lists_are_bounded(cls, value: list[str]) -> list[str]:
        if any(not item.strip() or len(item) > 500 for item in value):
            raise ValueError("Structured action values must contain text and be 500 characters or fewer.")
        return value


class InspectionEvidence(CamelModel):
    id: UUID
    crop_plan_request_id: UUID = Field(alias="cropPlanRequestId")
    field_id: UUID = Field(alias="fieldId")
    inspector_user_id: UUID = Field(alias="inspectorUserId")
    inspection_purpose: str = Field(alias="inspectionPurpose")
    status: str
    summary: str
    scheduled_at: datetime | None = Field(default=None, alias="scheduledAt")
    completed_at: datetime | None = Field(default=None, alias="completedAt")
    observations: list[dict[str, Any]] = Field(default_factory=list)
