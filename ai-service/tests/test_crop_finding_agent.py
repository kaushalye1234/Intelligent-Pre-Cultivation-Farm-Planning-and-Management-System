import json
from types import SimpleNamespace
from uuid import uuid4

import pytest

from agents.crop_finding_agent import CropFindingAgent
from config import Settings
from providers.base_llm_provider import BaseLLMProvider, LLMResponse, WebSearchResponse, WebSearchSource
from schemas.crop_finding import DiscoverReferencesInput, SuggestCropsInput, SuggestVarietiesInput
from tools.crop_finding_tools import ExtractedSegment, RetrievedDocument, SourcePolicy


def document(stage: int, source_id: str, text: str) -> RetrievedDocument:
    return RetrievedDocument(
        source_id=source_id,
        title="Official crop guide",
        organization_name="Department of Agriculture" if stage == 1 else "FAO",
        original_url="https://doa.gov.lk/guide" if stage == 1 else "https://www.fao.org/guide",
        final_url="https://doa.gov.lk/guide" if stage == 1 else "https://www.fao.org/guide",
        source_category="Government" if stage == 1 else "International",
        country="Sri Lanka" if stage == 1 else "International",
        stage=stage,
        content_type="text/html",
        retrieval_status="Retrieved",
        retrieved_at="2026-10-01T00:00:00+00:00",
        acceptance_reason="Approved test source",
        segments=[ExtractedSegment(text=text, section="Varieties")],
    )


class FakeTools:
    def __init__(self, stage_documents):
        self.settings = Settings(
            _env_file=None,
            AI_PROVIDER="openai",
            AI_MODEL="gpt-6-luna",
            OPENAI_API_KEY="test",
        )
        self.source_policy = SourcePolicy.load_default()
        self.stage_documents = stage_documents

    async def retrieve_many(self, candidates, stage, limit):
        return self.stage_documents.get(stage, [])[:limit], []


class FakeProvider(BaseLLMProvider):
    provider_name = "openai"

    def __init__(self, outputs):
        self.outputs = list(outputs)
        self.search_stages = []

    async def search_web(self, prompt, allowed_domains, max_results):
        stage = 1 if "doa.gov.lk" in allowed_domains else 2
        self.search_stages.append(stage)
        url = "https://doa.gov.lk/guide" if stage == 1 else "https://www.fao.org/guide"
        return WebSearchResponse(sources=[WebSearchSource(url=url, title="Guide")])

    async def generate_json(self, prompt, response_schema=None):
        return LLMResponse(text=json.dumps(self.outputs.pop(0)))


@pytest.mark.asyncio
async def test_stage_two_runs_only_when_sri_lankan_evidence_has_a_gap():
    local_text = "Rice is cultivated in Sri Lanka according to this official guide."
    provider = FakeProvider([
        {"suggestions": [], "analysis": ["No explicit crop list."], "recommendations": []},
        {"suggestions": [{
            "name": "Rice", "description": "Tropical cereal", "evidenceStatus": "Supported",
            "explanation": "The fallback source names rice.", "sourceId": "intl",
            "evidenceText": "Rice is a major tropical crop.", "pageNumber": None,
            "section": "Varieties", "warnings": []
        }], "analysis": ["Extracted one explicit crop name."], "recommendations": []},
    ])
    tools = FakeTools({
        1: [document(1, "local", local_text)],
        2: [document(2, "intl", "Rice is a major tropical crop.")],
    })

    result = await CropFindingAgent(tools, provider).suggest_crops(
        SuggestCropsInput(adminUserId=uuid4(), maxSuggestions=5)
    )

    assert provider.search_stages == [1, 2]
    assert result.used_international_fallback is True
    assert result.suggestions[0].evidence_status == "Partially Supported"
    assert result.suggestions[0].provenance[0].source_classification == "International fallback"
    assert result.analysis == [
        "Sri Lankan evidence: No explicit crop list.",
        "International fallback: Extracted one explicit crop name.",
    ]


@pytest.mark.asyncio
async def test_supported_sri_lankan_evidence_stops_before_stage_two():
    local_text = "Rice is cultivated in Sri Lanka according to this official guide."
    provider = FakeProvider([{
        "suggestions": [{
            "name": "Rice", "description": "Cereal crop", "evidenceStatus": "Supported",
            "explanation": "The Sri Lankan source explicitly names rice.", "sourceId": "local",
            "evidenceText": local_text, "pageNumber": None, "section": "Varieties", "warnings": []
        }],
        "analysis": ["Authoritative Sri Lankan evidence was sufficient."],
        "recommendations": [],
    }])
    tools = FakeTools({1: [document(1, "local", local_text)]})

    result = await CropFindingAgent(tools, provider).suggest_crops(
        SuggestCropsInput(adminUserId=uuid4(), maxSuggestions=5)
    )

    assert provider.search_stages == [1]
    assert result.used_international_fallback is False
    assert result.suggestions[0].evidence_status == "Supported"
    assert result.suggestions[0].provenance[0].source_classification == "Sri Lankan"
    assert result.analysis == ["Sri Lankan evidence: Authoritative Sri Lankan evidence was sufficient."]


@pytest.mark.asyncio
async def test_variety_analysis_distinguishes_missing_local_evidence_from_international_fallback():
    provider = FakeProvider([
        {
            "suggestions": [],
            "analysis": ["No explicit variety names for Chili appear in the supplied evidence."],
            "recommendations": ["Find a source that explicitly names Chili varieties."],
        },
        {
            "suggestions": [{
                "name": "Yummy Hot",
                "description": "Named chili variety",
                "evidenceStatus": "Supported",
                "explanation": "The international source explicitly names Yummy Hot.",
                "sourceId": "intl",
                "evidenceText": "This new variety, called Yummy Hot, is a high-yielding chili.",
                "pageNumber": None,
                "section": "Varieties",
                "warnings": [],
            }],
            "analysis": ["Extracted one explicitly named chili variety."],
            "recommendations": ["Treat the result as international fallback only."],
        },
    ])
    tools = FakeTools({
        1: [document(1, "local", "This Sri Lankan page discusses Capsicum cultivation without naming varieties.")],
        2: [document(2, "intl", "This new variety, called Yummy Hot, is a high-yielding chili.")],
    })

    result = await CropFindingAgent(tools, provider).suggest_varieties(
        SuggestVarietiesInput(
            adminUserId=uuid4(),
            cropTypeId=uuid4(),
            cropName="Chili",
            maxSuggestions=5,
        )
    )

    assert provider.search_stages == [1, 2]
    assert result.used_international_fallback is True
    assert result.analysis == [
        "Sri Lankan evidence: No explicit variety names for Chili appear in the supplied evidence.",
        "International fallback: Extracted one explicitly named chili variety.",
    ]
    assert result.recommendations == [
        "Sri Lankan evidence: Find a source that explicitly names Chili varieties.",
        "International fallback: Treat the result as international fallback only.",
    ]
    assert result.suggestions[0].evidence_status == "Partially Supported"
    assert result.suggestions[0].provenance[0].source_classification == "International fallback"
    assert any("does not establish Sri Lankan-specific applicability" in warning for warning in result.suggestions[0].warnings)


@pytest.mark.asyncio
async def test_reference_conflicts_remain_source_specific_and_resource_rules_are_removed():
    local_a = document(1, "source-a", "The establishment stage lasts 10 days. Maha planting is recommended.")
    local_b = document(1, "source-b", "The establishment stage lasts 14 days.")
    provider = FakeProvider([{
        "sourceDrafts": [
            {"sourceId": "source-a", "items": [
                {"field": "minimumDays", "suggestedValue": {"stageName": "Establishment", "days": 10}, "displayValue": "10 days", "evidenceStatus": "Supported", "explanation": "Explicit.", "evidenceText": "The establishment stage lasts 10 days.", "pageNumber": None, "section": "Varieties", "warnings": []},
                {"field": "structuredRule", "suggestedValue": {"ruleType": "ResourceRequirement", "ruleKey": "seed", "structuredValueJson": "{\"quantity\":10}"}, "displayValue": "Seed", "evidenceStatus": "Supported", "explanation": "Forbidden.", "evidenceText": "Maha planting is recommended.", "pageNumber": None, "section": "Varieties", "warnings": []}
            ], "analysis": [], "recommendations": []},
            {"sourceId": "source-b", "items": [
                {"field": "minimumDays", "suggestedValue": {"stageName": "Establishment", "days": 14}, "displayValue": "14 days", "evidenceStatus": "Supported", "explanation": "Explicit.", "evidenceText": "The establishment stage lasts 14 days.", "pageNumber": None, "section": "Varieties", "warnings": []}
            ], "analysis": [], "recommendations": []}
        ],
        "analysis": ["Two Sri Lankan sources were compared."], "recommendations": [], "unsupportedFields": []
    }])
    tools = FakeTools({1: [local_a, local_b]})

    result = await CropFindingAgent(tools, provider).discover_references(
        DiscoverReferencesInput(adminUserId=uuid4(), cropTypeId=uuid4(), cropName="Rice")
    )

    minimum_items = [item for draft in result.source_drafts for item in draft.items if item.field == "minimumDays"]
    assert len(minimum_items) == 2
    assert {item.evidence_status for item in minimum_items} == {"Conflict"}
    assert len({item.conflict_group_id for item in minimum_items}) == 1
    assert not any(item.field == "structuredRule" for draft in result.source_drafts for item in draft.items)
    assert provider.search_stages == [1, 2]  # missing growth/max fields triggered evidence-gap fallback
    assert result.analysis == [
        "Sri Lankan evidence: Two Sri Lankan sources were compared.",
        "International fallback: No approved source could be extracted automatically.",
    ]
