import json
from uuid import uuid4

import pytest
from fastapi.testclient import TestClient

import main
from agents.resource_requirement_research_agent import ResourceRequirementResearchAgent
from config import Settings, get_settings
from providers.base_llm_provider import BaseLLMProvider, LLMResponse, WebSearchResponse, WebSearchSource
from schemas.resource_requirement_research import ResourceRequirementResearchInput, ResourceRequirementResearchResponse
from tools.crop_finding_tools import ExtractedSegment, RetrievedDocument, SourcePolicy


# SAMPLE TEST DATA ONLY: the page text and rates below are fixtures, not agricultural recommendations.
CHILLI_PAGE = (
    "CHILLI Recommended varieties MI 2, MICH HY 1. Fertilizer recommendation for chilli: "
    "Urea (kg/ha) 65 65 65 TSP (kg/ha) 100 MOP (kg/ha) 50 25. "
    "GROUNDNUT Fertilizer: Urea kg/ha 35 basal, 30 at flowering."
)
UREA_ROW = "Urea (kg/ha) 65 65 65"


def document(stage: int, source_id: str, text: str, url: str | None = None) -> RetrievedDocument:
    default_url = "https://doa.gov.lk/fcrdi-crops/" if stage == 1 else "https://www.fao.org/guide"
    return RetrievedDocument(
        source_id=source_id,
        title="Crop fertilizer guide" if stage == 1 else "International guide",
        organization_name="Department of Agriculture" if stage == 1 else "FAO",
        original_url=url or default_url,
        final_url=url or default_url,
        source_category="Government" if stage == 1 else "International",
        country="Sri Lanka" if stage == 1 else "International",
        stage=stage,
        content_type="text/html",
        retrieval_status="Retrieved",
        retrieved_at="2026-10-05T00:00:00+00:00",
        acceptance_reason="Approved test source",
        segments=[ExtractedSegment(text=text, section="Crops")],
    )


class FakeTools:
    def __init__(self, stage_documents):
        self.settings = Settings(_env_file=None, AI_PROVIDER="openai", AI_MODEL="gpt-6-luna", OPENAI_API_KEY="test")
        self.source_policy = SourcePolicy.load_default()
        self.stage_documents = stage_documents

    async def retrieve_many(self, candidates, stage, limit):
        return self.stage_documents.get(stage, [])[:limit], []


class FakeProvider(BaseLLMProvider):
    provider_name = "openai"

    def __init__(self, outputs):
        self.outputs = list(outputs)
        self.search_stages = []
        self.search_domains = []
        self.prompts = []

    async def search_web(self, prompt, allowed_domains, max_results):
        stage = 1 if "doa.gov.lk" in allowed_domains else 2
        self.search_stages.append(stage)
        self.search_domains.append(list(allowed_domains))
        url = "https://doa.gov.lk/fcrdi-crops/" if stage == 1 else "https://www.fao.org/guide"
        return WebSearchResponse(sources=[WebSearchSource(url=url, title="Guide")])

    async def generate_json(self, prompt, response_schema=None):
        self.prompts.append(prompt)
        return LLMResponse(text=json.dumps(self.outputs.pop(0)))


def rate(source_id="local", components=None, crop_context="Fertilizer recommendation for chilli", unit="kg", area="hectare"):
    return {
        "sourceId": source_id,
        "resourceUnit": unit,
        "areaUnit": area,
        "components": components if components is not None else [
            {"label": "Basal", "quantity": 65, "evidenceText": UREA_ROW},
            {"label": "Top dressing 1", "quantity": 65, "evidenceText": UREA_ROW},
            {"label": "Top dressing 2", "quantity": 65, "evidenceText": UREA_ROW},
        ],
        "cropContextText": crop_context,
        "pageNumber": None,
        "section": "Crops",
        "explanation": "Table row for chilli.",
        "warnings": [],
    }


def payload(*recommendations):
    return {"recommendations": list(recommendations), "analysis": [], "warnings": []}


def research_input(resource_name="Urea", resource_unit="kg", crop_name="Chili", **overrides):
    values = {
        "actorUserId": str(uuid4()),
        "cropTypeId": str(uuid4()),
        "cropName": crop_name,
        "resourceId": str(uuid4()),
        "resourceName": resource_name,
        "resourceUnit": resource_unit,
        "otherCropNames": ["Onion", "Rice"],
    }
    values.update(overrides)
    return ResourceRequirementResearchInput.model_validate(values)


async def run(stage_documents, outputs, **input_overrides):
    provider = FakeProvider(outputs)
    agent = ResourceRequirementResearchAgent(FakeTools(stage_documents), provider)
    result = await agent.research_resource_requirement(research_input(**input_overrides))
    return result, provider


@pytest.mark.asyncio
async def test_verified_rate_returns_pending_verification_with_source_and_evidence():
    result, provider = await run({1: [document(1, "local", CHILLI_PAGE)]}, [payload(rate())])

    assert result.status == "PendingVerification"
    assert result.verified is False
    assert result.suggested_quantity_per_area == 195
    assert (result.suggested_resource_unit, result.suggested_area_unit) == ("kg", "hectare")
    assert result.source_url == "https://doa.gov.lk/fcrdi-crops/"
    assert UREA_ROW in (result.evidence or "")
    recommendation = result.recommendations[0]
    assert recommendation.basis == "65 + 65 + 65 = 195 kg/hectare"
    assert recommendation.evidence_status == "Supported"
    assert recommendation.unit_matches_inventory is True
    # Only the approved Sri Lankan stage was searched, through the existing provider.search_web.
    assert provider.search_stages == [1]
    assert "doa.gov.lk" in provider.search_domains[0]
    assert "Never invent numbers" in provider.prompts[0]


@pytest.mark.asyncio
async def test_no_rate_in_any_source_returns_no_verified_recommendation_and_no_value():
    result, provider = await run(
        {1: [document(1, "local", CHILLI_PAGE)], 2: [document(2, "intl", "General guidance only.")]},
        [payload(), payload()],
    )

    assert result.status == "NoVerifiedRecommendationFound"
    assert result.suggested_quantity_per_area is None
    assert result.recommendations == []
    assert provider.search_stages == [1, 2]


@pytest.mark.asyncio
async def test_excerpt_not_in_source_text_is_rejected():
    invented = rate(components=[{"label": "Basal", "quantity": 80, "evidenceText": "Urea (kg/ha) 80"}])
    result, _ = await run({1: [document(1, "local", CHILLI_PAGE)], 2: []}, [payload(invented)])

    assert result.status == "EvidenceValidationFailed"
    assert result.suggested_quantity_per_area is None
    assert "could not be matched" in result.rejected_claims[0]


@pytest.mark.asyncio
async def test_quantity_missing_from_its_excerpt_is_rejected():
    wrong_number = rate(components=[{"label": "Basal", "quantity": 100, "evidenceText": UREA_ROW}])
    result, _ = await run({1: [document(1, "local", CHILLI_PAGE)], 2: []}, [payload(wrong_number)])

    assert result.status == "EvidenceValidationFailed"
    assert "does not appear in its evidence excerpt" in result.rejected_claims[0]


@pytest.mark.asyncio
async def test_rate_printed_under_another_crop_heading_is_rejected():
    groundnut = rate(
        components=[{"label": "Basal", "quantity": 35, "evidenceText": "Urea kg/ha 35 basal"}],
        crop_context="CHILLI Recommended varieties",
    )
    result, _ = await run({1: [document(1, "local", CHILLI_PAGE)], 2: []}, [payload(groundnut)])

    assert result.status == "EvidenceValidationFailed"
    assert "another crop is named" in result.rejected_claims[0]


@pytest.mark.asyncio
async def test_rate_that_does_not_name_the_resource_is_rejected():
    tsp_row = rate(components=[{"label": "Basal", "quantity": 100, "evidenceText": "TSP (kg/ha) 100"}])
    result, _ = await run({1: [document(1, "local", CHILLI_PAGE)], 2: []}, [payload(tsp_row)])

    assert result.status == "EvidenceValidationFailed"
    assert "do not name Urea" in result.rejected_claims[0]


@pytest.mark.asyncio
async def test_resource_alias_is_accepted_for_mop():
    page = "Chilli fertilizer: Muriate of potash 50 kg/ha basal."
    mop = rate(components=[{"label": "Basal", "quantity": 50, "evidenceText": "Muriate of potash 50 kg/ha basal"}], crop_context="Chilli fertilizer")
    result, _ = await run({1: [document(1, "local", page)]}, [payload(mop)], resource_name="MOP")

    assert result.status == "PendingVerification"
    assert result.suggested_quantity_per_area == 50


@pytest.mark.asyncio
async def test_conflicting_sources_are_returned_without_a_preselected_value():
    other_page = "Chilli fertilizer recommendation: Urea 150 kg/ha in total."
    other = rate(
        source_id="other",
        components=[{"label": "Total", "quantity": 150, "evidenceText": "Urea 150 kg/ha in total"}],
        crop_context="Chilli fertilizer recommendation",
    )
    result, _ = await run(
        {1: [document(1, "local", CHILLI_PAGE), document(1, "other", other_page, "https://doa.gov.lk/hordi-chilli/")]},
        [payload(rate(), other)],
    )

    assert result.status == "ConflictingSources"
    assert result.suggested_quantity_per_area is None
    assert sorted(item.quantity_per_area for item in result.recommendations) == [150, 195]


@pytest.mark.asyncio
async def test_acre_and_hectare_rates_that_agree_are_not_a_conflict():
    acre_page = "Chilli: Urea 78.914 kg/ac for the season."
    acre = rate(
        source_id="acre",
        components=[{"label": "Season", "quantity": 78.914, "evidenceText": "Urea 78.914 kg/ac for the season"}],
        crop_context="Chilli",
        area="acre",
    )
    result, _ = await run(
        {1: [document(1, "local", CHILLI_PAGE), document(1, "acre", acre_page, "https://doa.gov.lk/hordi-chilli/")]},
        [payload(rate(), acre)],
    )

    assert result.status == "PendingVerification"
    assert result.suggested_quantity_per_area == 195


@pytest.mark.asyncio
async def test_unit_that_differs_from_inventory_is_not_suggested():
    result, _ = await run({1: [document(1, "local", CHILLI_PAGE)], 2: []}, [payload(rate())], resource_unit="bag")

    assert result.status == "NoVerifiedRecommendationFound"
    assert result.suggested_quantity_per_area is None
    assert result.recommendations[0].unit_matches_inventory is False
    assert any("inventory unit bag" in warning for warning in result.warnings)


@pytest.mark.asyncio
async def test_international_fallback_is_downgraded():
    fao_page = "Chilli pepper nutrient guide: Urea 120 kg/ha split in two applications."
    fao = rate(
        source_id="intl",
        components=[{"label": "Season", "quantity": 120, "evidenceText": "Urea 120 kg/ha split in two applications"}],
        crop_context="Chilli pepper nutrient guide",
    )
    result, provider = await run(
        {1: [document(1, "local", "No fertilizer data.")], 2: [document(2, "intl", fao_page)]},
        [payload(), payload(fao)],
    )

    assert provider.search_stages == [1, 2]
    assert result.status == "PendingVerification"
    assert result.used_international_fallback is True
    assert result.recommendations[0].evidence_status == "Partially Supported"
    assert any("Sri Lankan-specific" in warning for warning in result.recommendations[0].warnings)


def test_research_endpoint_requires_service_token_and_never_echoes_secrets(monkeypatch):
    settings = Settings(
        _env_file=None,
        AI_SERVICE_TOKEN="test-service-token",
        AI_PROVIDER="openai",
        AI_MODEL="gpt-6-luna",
        OPENAI_API_KEY="test-openai-key-value",
        BACKEND_TOOL_TOKEN="test-backend-tool-token",
    )
    main.app.dependency_overrides[get_settings] = lambda: settings
    body = research_input().model_dump(mode="json", by_alias=True)

    async def fake_research(self, request):
        return ResourceRequirementResearchResponse(
            requestId="request-1",
            status="NoVerifiedRecommendationFound",
            cropTypeId=request.crop_type_id,
            cropName=request.crop_name,
            resourceId=request.resource_id,
            resourceName=request.resource_name,
            resourceUnit=request.resource_unit,
        )

    monkeypatch.setattr(main.ResourceRequirementResearchAgent, "research_resource_requirement", fake_research)
    try:
        with TestClient(main.app) as client:
            path = "/crop-finding/resource-requirement-research"
            assert client.post(path, json=body).status_code == 401
            headers = {"Authorization": "Bearer test-service-token"}
            response = client.post(path, json=body, headers=headers)
            assert response.status_code == 200
            assert response.json()["status"] == "NoVerifiedRecommendationFound"
            assert response.json()["verified"] is False
            for secret in ("test-service-token", "test-openai-key-value", "test-backend-tool-token"):
                assert secret not in response.text
            invalid = client.post(path, json={**body, "resourceName": ""}, headers=headers)
            assert invalid.status_code == 422
    finally:
        main.app.dependency_overrides.clear()
