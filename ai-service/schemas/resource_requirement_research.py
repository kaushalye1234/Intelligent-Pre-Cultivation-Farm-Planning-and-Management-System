from typing import Annotated, Literal
from uuid import UUID

from pydantic import Field

from schemas.common import CamelModel
from schemas.crop_finding import DiscoveredSource, EvidenceProvenance


# PendingVerification: one evidence-checked value (or several sources that agree); an Admin must still verify it.
# ConflictingSources: evidence-checked sources disagree; no value is preselected.
# NoVerifiedRecommendationFound: no approved source stated a usable rate for this crop and resource.
# EvidenceValidationFailed: rates were proposed but none survived the evidence checks.
ResearchStatus = Literal[
    "PendingVerification",
    "ConflictingSources",
    "NoVerifiedRecommendationFound",
    "EvidenceValidationFailed",
]
AreaUnit = Literal["acre", "hectare"]


class ResourceRequirementResearchInput(CamelModel):
    actor_user_id: UUID
    crop_type_id: UUID
    crop_name: str = Field(min_length=1, max_length=120)
    crop_variety_id: UUID | None = None
    variety_name: str | None = Field(default=None, max_length=120)
    region: str | None = Field(default=None, max_length=120)
    resource_id: UUID
    resource_name: str = Field(min_length=1, max_length=160)
    resource_unit: str = Field(min_length=1, max_length=40)
    # Other crop names from the backend catalogue, used to reject a rate printed under another crop's heading.
    other_crop_names: list[Annotated[str, Field(max_length=120)]] = Field(default_factory=list, max_length=200)


class RequirementComponent(CamelModel):
    label: str = Field(max_length=160)
    quantity: float = Field(gt=0)
    evidence_text: str = Field(max_length=1200)


class ResourceRequirementRecommendation(CamelModel):
    id: str
    quantity_per_area: float = Field(gt=0)
    resource_unit: str = Field(max_length=40)
    area_unit: AreaUnit
    unit_matches_inventory: bool
    basis: str = Field(max_length=600)
    components: list[RequirementComponent]
    evidence_status: Literal["Supported", "Partially Supported"]
    crop_context: str = Field(default="", max_length=1200)
    source: EvidenceProvenance
    warnings: list[str] = Field(default_factory=list)


class ResourceRequirementResearchResponse(CamelModel):
    request_id: str
    status: ResearchStatus
    verified: Literal[False] = False
    crop_type_id: UUID
    crop_name: str
    crop_variety_id: UUID | None = None
    variety_name: str | None = None
    region: str | None = None
    resource_id: UUID
    resource_name: str
    resource_unit: str
    suggested_quantity_per_area: float | None = None
    suggested_resource_unit: str | None = None
    suggested_area_unit: AreaUnit | None = None
    source_name: str | None = None
    source_url: str | None = None
    evidence: str | None = None
    used_international_fallback: bool = False
    recommendations: list[ResourceRequirementRecommendation] = Field(default_factory=list)
    rejected_claims: list[str] = Field(default_factory=list)
    sources: list[DiscoveredSource] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
