from datetime import datetime
from typing import Any, Literal
from uuid import UUID

from pydantic import ConfigDict, Field, field_validator

from schemas.common import AgentEnvelope, CamelModel
from schemas.inspection_image_analysis import CropHealthActionType, ValidatedSourceReference

CROP_FIELD_ANALYSIS_CONTRACT_VERSION = 2
REVIEWED_IMAGE_ANALYSIS_CONTRACT_VERSION = 1


class ReviewedCropHealthActionInput(CamelModel):
    model_config = ConfigDict(extra="forbid")
    action_type: CropHealthActionType = Field(alias="actionType")
    order: int = Field(ge=0, le=6)
    origin: Literal["AiSuggested", "OfficerAdded"]
    source_policy_ids: list[str] = Field(alias="sourcePolicyIds", max_length=3)


class ReviewedImageAnalysisInputProjection(CamelModel):
    model_config = ConfigDict(extra="forbid")
    contract_version: Literal[1] = Field(default=REVIEWED_IMAGE_ANALYSIS_CONTRACT_VERSION, alias="contractVersion")
    analysis_id: UUID = Field(alias="analysisId")
    inspection_image_id: UUID = Field(alias="inspectionImageId")
    visible_findings: list[str] = Field(alias="visibleFindings", max_length=8)
    possible_concerns: list[str] = Field(alias="possibleConcerns", max_length=5)
    severity: Literal["Low", "Moderate", "High", "Unknown"]
    uncertainty: str = Field(max_length=600)
    actions: list[ReviewedCropHealthActionInput] = Field(max_length=7)
    source_references: list[ValidatedSourceReference] = Field(alias="sourceReferences", max_length=3)
    requires_further_assessment: bool = Field(alias="requiresFurtherAssessment")
    officer_edited_fields: list[str] = Field(alias="officerEditedFields", max_length=6)


class FieldAnalysisInput(CamelModel):
    workflow_id: UUID = Field(alias="workflowId")
    crop_plan_request_id: UUID = Field(alias="cropPlanRequestId")
    pre_planting_inspection_id: UUID = Field(alias="prePlantingInspectionId")
    field_id: UUID = Field(alias="fieldId")
    crop_cycle_id: UUID | None = Field(default=None, alias="cropCycleId")
    requested_analysis: list[str] = Field(default_factory=list, alias="requestedAnalysis")
    crop_reference_profile_id: UUID | None = Field(default=None, alias="cropReferenceProfileId")
    agent_step_id: UUID | None = Field(default=None, alias="agentStepId")
    reviewed_image_analysis: ReviewedImageAnalysisInputProjection | None = Field(default=None, alias="reviewedImageAnalysis")

    @field_validator("requested_analysis")
    @classmethod
    def requested_analysis_is_bounded(cls, value: list[str]) -> list[str]:
        cleaned = [item.strip() for item in value if item.strip()]
        if len(cleaned) > 12:
            raise ValueError("Requested analysis must contain 12 items or fewer.")
        return cleaned


class FieldCondition(CamelModel):
    model_config = ConfigDict(extra="forbid")

    summary: str
    evidence_inspection_ids: list[UUID] = Field(default_factory=list, alias="evidenceInspectionIds")


class OpenIssueSummary(CamelModel):
    model_config = ConfigDict(extra="forbid")

    issue_id: UUID = Field(alias="issueId")
    severity: str
    status: str
    evidence_inspection_id: UUID | None = Field(default=None, alias="evidenceInspectionId")


class CropFieldAnalysisOutput(AgentEnvelope):
    model_config = ConfigDict(extra="forbid")

    contract_version: Literal[2] = Field(default=CROP_FIELD_ANALYSIS_CONTRACT_VERSION, alias="contractVersion")
    status: Literal["Analyzed", "SafeFailure"]
    field_condition: FieldCondition = Field(alias="fieldCondition")
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
