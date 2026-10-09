from typing import Any, Literal
from uuid import UUID

from pydantic import Field, field_validator

from schemas.common import CamelModel


EvidenceStatus = Literal["Supported", "Partially Supported", "Unsupported", "Conflict", "Manual Review Required"]
SourceClassification = Literal["Sri Lankan", "International fallback"]
RetrievalStatus = Literal["Retrieved", "Manual Review Required", "Failed"]
CropFindingOperation = Literal["web_search", "source_retrieval", "structured_analysis"]


class CropFindingErrorDetail(CamelModel):
    code: str = Field(min_length=1, max_length=120)
    message: str = Field(min_length=1, max_length=800)
    request_id: str = Field(min_length=1, max_length=200)
    operation: CropFindingOperation | None = None
    stage: int | None = Field(default=None, ge=1, le=2)
    attempt: int | None = Field(default=None, ge=1)
    category: str | None = Field(default=None, max_length=120)
    upstream_status: int | None = Field(default=None, ge=100, le=599)
    provider_error_code: str | None = Field(default=None, max_length=120)
    provider_request_id: str | None = Field(default=None, max_length=200)
    configured_timeout_seconds: float | None = Field(default=None, gt=0)
    effective_timeout_seconds: float | None = Field(default=None, ge=0)


class SuggestCropsInput(CamelModel):
    admin_user_id: UUID
    context: str | None = Field(default=None, max_length=300)
    max_suggestions: int = Field(default=8, ge=1, le=10)


class SuggestVarietiesInput(CamelModel):
    admin_user_id: UUID
    crop_type_id: UUID
    crop_name: str = Field(min_length=1, max_length=120)
    context: str | None = Field(default=None, max_length=300)
    max_suggestions: int = Field(default=8, ge=1, le=10)


class DiscoverReferencesInput(CamelModel):
    admin_user_id: UUID
    crop_type_id: UUID
    crop_name: str = Field(min_length=1, max_length=120)
    crop_variety_id: UUID | None = None
    variety_name: str | None = Field(default=None, max_length=120)
    region: str | None = Field(default=None, max_length=120)


class EvidenceProvenance(CamelModel):
    source_id: str
    source_name: str
    organization_name: str
    original_url: str
    final_url: str
    source_category: str
    country: str
    source_classification: SourceClassification
    stage: int
    evidence_text: str = Field(default="", max_length=1200)
    page_number: int | None = Field(default=None, ge=1)
    section: str | None = Field(default=None, max_length=240)


class DiscoveredSource(CamelModel):
    source_id: str
    title: str
    organization_name: str
    original_url: str
    final_url: str
    source_category: str
    country: str
    source_classification: SourceClassification
    stage: int
    content_type: str | None = None
    retrieval_status: RetrievalStatus
    retrieved_at: str | None = None
    acceptance_reason: str
    rejection_reason: str | None = None
    manual_review_required: bool = False
    page_count: int | None = None
    warnings: list[str] = Field(default_factory=list)
    existing_reference: bool = False
    existing_reference_id: UUID | None = None


class CropSuggestion(CamelModel):
    id: str
    name: str = Field(min_length=1, max_length=120)
    description: str | None = Field(default=None, max_length=500)
    evidence_status: EvidenceStatus
    explanation: str = Field(max_length=800)
    provenance: list[EvidenceProvenance] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    already_exists: bool = False
    existing_crop_type_id: UUID | None = None


class VarietySuggestion(CamelModel):
    id: str
    name: str = Field(min_length=1, max_length=120)
    description: str | None = Field(default=None, max_length=500)
    evidence_status: EvidenceStatus
    explanation: str = Field(max_length=800)
    provenance: list[EvidenceProvenance] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    already_exists: bool = False
    existing_crop_variety_id: UUID | None = None


class ReferenceDraftItem(CamelModel):
    id: str
    field: Literal[
        "sourceName", "sourceUrl", "sourceVersion", "region", "growthStage",
        "minimumDays", "maximumDays", "evidenceNotes", "structuredRule"
    ]
    suggested_value: Any = None
    display_value: str = Field(default="", max_length=1000)
    evidence_status: EvidenceStatus
    explanation: str = Field(max_length=800)
    provenance: list[EvidenceProvenance] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    conflict_group_id: str | None = None

    @field_validator("suggested_value")
    @classmethod
    def reject_resource_rules(cls, value: Any) -> Any:
        if isinstance(value, dict):
            rule_type = str(value.get("ruleType", "")).replace("_", "").replace("-", "").lower()
            serialized = str(value).replace("_", "").replace("-", "").lower()
            forbidden = ("resourcerequirement", "fertilizerquantity", "irrigationquantity", "seedquantity", "inventory")
            if rule_type == "resourcerequirement" or any(term in serialized for term in forbidden):
                raise ValueError("Resource and inventory rules are not permitted in CropFinding drafts.")
        return value


class ReferenceSourceDraft(CamelModel):
    source: DiscoveredSource
    items: list[ReferenceDraftItem] = Field(default_factory=list)
    analysis: list[str] = Field(default_factory=list)
    recommendations: list[str] = Field(default_factory=list)


class CropSuggestionsResponse(CamelModel):
    request_id: str
    action: Literal["SuggestCrops"] = "SuggestCrops"
    used_international_fallback: bool
    sources: list[DiscoveredSource]
    suggestions: list[CropSuggestion]
    analysis: list[str]
    recommendations: list[str]
    warnings: list[str] = Field(default_factory=list)


class VarietySuggestionsResponse(CamelModel):
    request_id: str
    action: Literal["SuggestVarieties"] = "SuggestVarieties"
    crop_type_id: UUID
    crop_name: str
    used_international_fallback: bool
    sources: list[DiscoveredSource]
    suggestions: list[VarietySuggestion]
    analysis: list[str]
    recommendations: list[str]
    warnings: list[str] = Field(default_factory=list)


class ReferenceDiscoveryResponse(CamelModel):
    request_id: str
    action: Literal["DiscoverReferences"] = "DiscoverReferences"
    crop_type_id: UUID
    crop_name: str
    crop_variety_id: UUID | None = None
    variety_name: str | None = None
    used_international_fallback: bool
    source_drafts: list[ReferenceSourceDraft]
    analysis: list[str]
    recommendations: list[str]
    unsupported_fields: list[str] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
