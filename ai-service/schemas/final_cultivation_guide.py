import re
from datetime import date, datetime
from uuid import UUID

from pydantic import Field, model_validator

from schemas.common import CamelModel


class FinalGuideActivity(CamelModel):
    id: UUID
    kind: str = Field(min_length=1, max_length=40)
    title: str = Field(min_length=1, max_length=180)
    scheduled_at: datetime | None = None


class FinalCultivationGuideInput(CamelModel):
    contract_version: int = Field(default=1, alias="contractVersion")
    workflow_id: UUID = Field(alias="workflowId")
    approved_revision: int = Field(alias="approvedRevision", ge=1)
    crop_plan_request_id: UUID = Field(alias="cropPlanRequestId")
    crop_name: str = Field(alias="cropName", min_length=1, max_length=120)
    variety_name: str | None = Field(default=None, alias="varietyName", max_length=120)
    farm_name: str | None = Field(default=None, alias="farmName", max_length=120)
    field_name: str | None = Field(default=None, alias="fieldName", max_length=120)
    location: str | None = Field(default=None, max_length=160)
    preferred_start_date: date = Field(alias="preferredStartDate")
    preferred_end_date: date = Field(alias="preferredEndDate")
    current_date: date = Field(alias="currentDate")
    evidence_summary: list[str] = Field(default_factory=list, alias="evidenceSummary", max_length=40)
    approved_activities: list[FinalGuideActivity] = Field(default_factory=list, alias="approvedActivities", max_length=200)


class MonthlyGuideAdvice(CamelModel):
    month: str = Field(pattern=r"^\d{4}-(0[1-9]|1[0-2])$")
    summary: str = Field(min_length=1, max_length=500)
    field_advice: list[str] = Field(default_factory=list, alias="fieldAdvice", max_length=8)
    weather_advice: list[str] = Field(default_factory=list, alias="weatherAdvice", max_length=8)


class FinalCultivationGuideOutput(CamelModel):
    contract_version: int = Field(default=1, alias="contractVersion")
    workflow_id: UUID = Field(alias="workflowId")
    approved_revision: int = Field(alias="approvedRevision", ge=1)
    weekly_guidance: list[str] = Field(default_factory=list, alias="weeklyGuidance", max_length=8)
    current_stage_explanation: str | None = Field(default=None, alias="currentStageExplanation", max_length=500)
    monthly_guidance: list[MonthlyGuideAdvice] = Field(default_factory=list, alias="monthlyGuidance", max_length=24)
    risks: list[str] = Field(default_factory=list, max_length=12)
    harvest_preparation: list[str] = Field(default_factory=list, alias="harvestPreparation", max_length=8)
    why_this_plan: str = Field(alias="whyThisPlan", min_length=1, max_length=700)

    @model_validator(mode="after")
    def reject_numeric_advice(self):
        narrative = [*self.weekly_guidance, self.current_stage_explanation or "", self.why_this_plan,
                     *self.risks, *self.harvest_preparation]
        for month in self.monthly_guidance:
            narrative.extend([month.summary, *month.field_advice, *month.weather_advice])
        if any(re.search(r"\d", value) for value in narrative):
            raise ValueError("Guide narrative cannot contain numeric guidance or dates.")
        return self
