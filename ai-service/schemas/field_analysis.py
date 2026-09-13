from datetime import datetime
from typing import Any
from uuid import UUID

from pydantic import Field, field_validator

from schemas.common import AgentEnvelope, CamelModel


class FieldAnalysisInput(CamelModel):
    workflow_id: UUID = Field(alias="workflowId")
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
    priority: str = "Unknown"


class InspectionEvidence(CamelModel):
    id: UUID
    field_id: UUID = Field(alias="fieldId")
    status: str
    summary: str
    scheduled_at: datetime | None = Field(default=None, alias="scheduledAt")
    completed_at: datetime | None = Field(default=None, alias="completedAt")
    observations: list[dict[str, Any]] = Field(default_factory=list)

