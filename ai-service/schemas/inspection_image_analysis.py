from typing import Literal
from uuid import UUID

from pydantic import ConfigDict, Field, field_validator, model_validator

from schemas.common import CamelModel, to_camel

INSPECTION_IMAGE_PASS1_CONTRACT_VERSION = 1
INSPECTION_IMAGE_ANALYSIS_CONTRACT_VERSION = 1
IMAGE_PREPROCESSING_VERSION = 1
INSPECTION_IMAGE_PROMPT_CONTRACT_VERSION = 1

IssueCategory = Literal[
    "Pest", "Fungal", "Bacterial", "DiseaseLike", "NutrientStress",
    "EnvironmentalStress", "PhysicalDamage", "Other", "Unknown",
]
CropHealthActionType = Literal[
    "FieldSanitation", "RemoveAffectedResidue", "SeparateAffectedMaterial",
    "InspectNearbyPlants", "MonitorSymptoms", "PrePlantingCleanup", "RequestFurtherAssessment",
]


class StrictCamelModel(CamelModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="forbid")


class InspectionImageAnalysisInput(StrictCamelModel):
    contract_version: Literal[1]
    analysis_id: UUID
    image_preprocessing_version: Literal[1]
    crop_name: str = Field(min_length=1, max_length=120)
    variety_name: str | None = Field(default=None, max_length=120)


class SearchIntent(StrictCamelModel):
    issue_category: IssueCategory
    is_primary: bool
    terms: list[str] = Field(min_length=1, max_length=5)

    @field_validator("terms")
    @classmethod
    def terms_are_short(cls, value: list[str]) -> list[str]:
        if any(not term.strip() or len(term) > 80 for term in value):
            raise ValueError("Search terms must be non-empty and 80 characters or fewer.")
        return value


class InspectionImagePass1Result(StrictCamelModel):
    contract_version: Literal[1]
    visible_findings: list[str] = Field(max_length=8)
    possible_issue_categories: list[IssueCategory] = Field(max_length=3)
    severity_indicators: list[str] = Field(max_length=5)
    uncertainty: str = Field(min_length=1, max_length=600)
    requires_further_assessment: bool
    search_intents: list[SearchIntent] = Field(max_length=3)

    @model_validator(mode="after")
    def has_at_most_one_primary_intent(self) -> "InspectionImagePass1Result":
        if sum(intent.is_primary for intent in self.search_intents) > 1:
            raise ValueError("At most one search intent may be primary.")
        return self


class EvidenceRelevanceSignals(StrictCamelModel):
    total_score: int
    crop_match: bool
    issue_match: bool
    title_score: int
    heading_score: int
    url_score: int
    body_score: int
    strong_evidence: bool


class GroundingEvidence(StrictCamelModel):
    source_policy_id: str = Field(min_length=1, max_length=160)
    source_stage: Literal["Stage1", "Stage2"]
    organization: str = Field(min_length=1, max_length=200)
    source_category: str = Field(min_length=1, max_length=120)
    final_url: str = Field(min_length=1, max_length=2000)
    document_title: str = Field(min_length=1, max_length=300)
    retrieved_at: str = Field(min_length=1, max_length=80)
    normalized_document_sha256: str = Field(pattern=r"^[a-f0-9]{64}$")
    relevance_signals: EvidenceRelevanceSignals
    relevance_rule_version: int = Field(ge=1)
    exact_extract: str = Field(min_length=1, max_length=8000)


class ValidatedSourceReference(StrictCamelModel):
    source_policy_id: str = Field(min_length=1, max_length=160)
    organization: str = Field(min_length=1, max_length=200)
    title: str = Field(min_length=1, max_length=300)
    url: str = Field(min_length=1, max_length=2000)
    source_stage: Literal["Stage1", "Stage2"]


class InspectionImageAnalysisResult(StrictCamelModel):
    contract_version: Literal[1]
    visible_findings: list[str] = Field(max_length=8)
    possible_issue_category: IssueCategory
    possible_issues: list[str] = Field(max_length=5)
    severity: Literal["Low", "Moderate", "High", "Unknown"]
    uncertainty: str = Field(min_length=1, max_length=600)
    validated_source_references: list[ValidatedSourceReference] = Field(max_length=3)
    recommended_non_chemical_actions: list[CropHealthActionType] = Field(max_length=7)
    requires_further_assessment: bool
    grounding_status: Literal["Grounded", "Unavailable"]


class InspectionImageAnalysisOperationResponse(StrictCamelModel):
    contract_version: Literal[1]
    status: Literal["Succeeded", "Failed", "TimedOut"]
    pass1_result: InspectionImagePass1Result | None
    evidence_packet: list[GroundingEvidence]
    final_result: InspectionImageAnalysisResult | None
    failure_category: str | None = Field(max_length=100)
    failure_message: str | None = Field(max_length=300)


class InspectionImageAnalysisCapabilityResponse(StrictCamelModel):
    contract_version: Literal[1]
    image_preprocessing_version: Literal[1]
    prompt_contract_version: Literal[1]
    relevance_rule_version: Literal[1]
    source_policy_version: str = Field(min_length=1, max_length=80)
    source_policy_hash: str = Field(pattern=r"^[a-f0-9]{64}$")
    provider: str = Field(min_length=1, max_length=80)
    model: str = Field(min_length=1, max_length=160)
