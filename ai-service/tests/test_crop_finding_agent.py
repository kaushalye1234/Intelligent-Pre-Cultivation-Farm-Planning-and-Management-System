import json
from types import SimpleNamespace
from uuid import uuid4

import pytest

from agents.crop_finding_agent import CropFindingAgent, EvidenceBundle
from config import Settings
from providers.base_llm_provider import BaseLLMProvider, LLMProviderError, LLMResponse, WebSearchResponse, WebSearchSource
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
    def __init__(self, stage_documents, settings_overrides=None):
        self.settings = Settings(
            _env_file=None,
            AI_PROVIDER="openai",
            AI_MODEL="gpt-6-luna",
            OPENAI_API_KEY="test",
            **(settings_overrides or {}),
        )
        self.source_policy = SourcePolicy.load_default()
        self.stage_documents = stage_documents
        self.retrieve_calls = []

    async def retrieve_many(self, candidates, stage, limit):
        self.retrieve_calls.append(stage)
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


class ControlledProvider(BaseLLMProvider):
    provider_name = "openai"

    def __init__(self, search_outcomes=None, generate_outcomes=None):
        self.search_outcomes = list(search_outcomes or [])
        self.generate_outcomes = list(generate_outcomes or [])
        self.search_stages = []
        self.generate_calls = 0
        self.analysis_timeouts = []

    async def search_web(self, prompt, allowed_domains, max_results):
        stage = 1 if "doa.gov.lk" in allowed_domains else 2
        self.search_stages.append(stage)
        outcome = self.search_outcomes.pop(0)
        if isinstance(outcome, BaseException):
            raise outcome
        return outcome

    async def generate_json(self, prompt, response_schema=None):
        self.generate_calls += 1
        outcome = self.generate_outcomes.pop(0)
        if isinstance(outcome, BaseException):
            raise outcome
        if isinstance(outcome, str):
            return LLMResponse(text=outcome)
        return LLMResponse(text=json.dumps(outcome))

    async def generate_crop_finding_json(self, prompt, response_schema, timeout_seconds):
        self.analysis_timeouts.append(timeout_seconds)
        return await self.generate_json(prompt, response_schema=response_schema)


def evidence_bundle() -> EvidenceBundle:
    return EvidenceBundle(
        text="SOURCE_ID: local\nCONTENT:\nSafe evidence text.",
        source_count=1,
        chunk_count=1,
        extracted_character_count=19,
        per_source_character_counts={"local": 19},
    )


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


@pytest.mark.asyncio
async def test_stage_one_web_search_timeout_is_not_retried_or_followed_by_retrieval(caplog):
    caplog.set_level("WARNING", logger="agriassist.crop_finding")
    provider = ControlledProvider(search_outcomes=[TimeoutError("PROMPT_SECRET")])
    tools = FakeTools({})

    with pytest.raises(LLMProviderError) as captured:
        await CropFindingAgent(tools, provider, request_id="request-timeout").discover_references(
            DiscoverReferencesInput(adminUserId=uuid4(), cropTypeId=uuid4(), cropName="Chili")
        )

    error = captured.value
    assert error.category == "timeout"
    assert error.operation == "web_search"
    assert error.stage == 1
    assert error.attempt == 1
    assert provider.search_stages == [1]
    assert tools.retrieve_calls == []
    assert provider.generate_calls == 0
    assert "OpenAI web search exceeded the configured CropFinding timeout." in caplog.text
    assert "requestId=request-timeout" in caplog.text
    assert "PROMPT_SECRET" not in caplog.text


@pytest.mark.parametrize(
    ("category", "status_code", "root_exception_class"),
    [
        ("rate_limit", 429, "RateLimitError"),
        ("server_error", 500, "InternalServerError"),
        ("server_error", 503, "InternalServerError"),
        ("connection", None, "APIConnectionError"),
    ],
)
@pytest.mark.asyncio
async def test_retryable_web_search_failure_receives_one_controlled_retry(
    category,
    status_code,
    root_exception_class,
):
    failure = LLMProviderError(
        "safe provider failure",
        category=category,
        operation="web_search",
        status_code=status_code,
        root_exception_class=root_exception_class,
        retryable=True,
    )
    provider = ControlledProvider(search_outcomes=[failure, WebSearchResponse(sources=[])])
    tools = FakeTools({})

    documents, warnings = await CropFindingAgent(tools, provider)._discover(
        stage=1,
        query="Find crops",
        action="DiscoverReferences",
        request_id="request-retry",
    )

    assert documents == []
    assert warnings == []
    assert provider.search_stages == [1, 1]
    assert tools.retrieve_calls == [1]


@pytest.mark.asyncio
async def test_invalid_request_web_search_failure_is_not_retried_and_preserves_stage_two_metadata():
    failure = LLMProviderError(
        "OpenAI rejected the CropFinding request.",
        category="invalid_request",
        operation="web_search",
        status_code=400,
        error_code="unsupported_parameter",
        provider_request_id="provider-request-400",
        root_exception_class="BadRequestError",
        retryable=False,
    )
    provider = ControlledProvider(search_outcomes=[failure])
    tools = FakeTools({})

    with pytest.raises(LLMProviderError) as captured:
        await CropFindingAgent(tools, provider)._discover(
            stage=2,
            query="Find fallback evidence",
            action="DiscoverReferences",
            request_id="request-stage-two",
        )

    error = captured.value
    assert error.stage == 2
    assert error.attempt == 1
    assert error.status_code == 400
    assert error.error_code == "unsupported_parameter"
    assert error.provider_request_id == "provider-request-400"
    assert provider.search_stages == [2]
    assert tools.retrieve_calls == []


@pytest.mark.asyncio
async def test_structured_analysis_timeout_is_distinct_from_web_search_timeout():
    provider = ControlledProvider(
        search_outcomes=[WebSearchResponse(sources=[WebSearchSource(url="https://doa.gov.lk/guide", title="Guide")])],
        generate_outcomes=[TimeoutError()],
    )
    tools = FakeTools({1: [document(1, "local", "Chili has an explicitly described establishment stage in this guide.")]})

    with pytest.raises(LLMProviderError) as captured:
        await CropFindingAgent(tools, provider, request_id="request-analysis").discover_references(
            DiscoverReferencesInput(adminUserId=uuid4(), cropTypeId=uuid4(), cropName="Chili")
        )

    error = captured.value
    assert error.category == "timeout"
    assert error.operation == "structured_analysis"
    assert error.stage == 1
    assert str(error) == "OpenAI structured analysis exceeded the configured CropFinding timeout."
    assert provider.search_stages == [1]
    assert tools.retrieve_calls == [1]
    assert provider.generate_calls == 1
    assert provider.analysis_timeouts == [45]


@pytest.mark.asyncio
async def test_malformed_analysis_output_is_classified_without_retry():
    provider = ControlledProvider(
        search_outcomes=[WebSearchResponse(sources=[WebSearchSource(url="https://doa.gov.lk/guide", title="Guide")])],
        generate_outcomes=["not-json"],
    )
    tools = FakeTools({1: [document(1, "local", "Chili has an explicitly described establishment stage in this guide.")]})

    with pytest.raises(LLMProviderError) as captured:
        await CropFindingAgent(tools, provider, request_id="request-structured").discover_references(
            DiscoverReferencesInput(adminUserId=uuid4(), cropTypeId=uuid4(), cropName="Chili")
        )

    assert captured.value.category == "structured_output"
    assert captured.value.operation == "structured_analysis"
    assert captured.value.attempt == 1
    assert provider.generate_calls == 1


@pytest.mark.asyncio
async def test_structured_analysis_uses_configured_timeout_when_budget_is_sufficient():
    provider = ControlledProvider(generate_outcomes=[{}])
    agent = CropFindingAgent(
        FakeTools({}),
        provider,
        request_id="request-budget-full",
        operation_started_at=0,
        monotonic_clock=lambda: 10,
    )

    await agent._generate_payload(
        "PROMPT_SECRET",
        {"type": "object"},
        action="DiscoverReferences",
        stage=1,
        request_id="request-budget-full",
        evidence=evidence_bundle(),
    )

    assert provider.analysis_timeouts == [45]


@pytest.mark.asyncio
async def test_structured_analysis_timeout_is_reduced_to_remaining_safe_budget():
    provider = ControlledProvider(generate_outcomes=[{}])
    agent = CropFindingAgent(
        FakeTools({}),
        provider,
        request_id="request-budget-reduced",
        operation_started_at=0,
        monotonic_clock=lambda: 80,
    )

    await agent._generate_payload(
        "prompt",
        {"type": "object"},
        action="DiscoverReferences",
        stage=1,
        request_id="request-budget-reduced",
        evidence=evidence_bundle(),
    )

    assert provider.analysis_timeouts == [20]


@pytest.mark.asyncio
async def test_structured_analysis_does_not_start_without_safe_remaining_budget():
    provider = ControlledProvider(generate_outcomes=[{}])
    agent = CropFindingAgent(
        FakeTools({}),
        provider,
        request_id="request-budget-exhausted",
        operation_started_at=0,
        monotonic_clock=lambda: 101,
    )

    with pytest.raises(LLMProviderError) as captured:
        await agent._generate_payload(
            "PROMPT_SECRET",
            {"type": "object"},
            action="DiscoverReferences",
            stage=1,
            request_id="request-budget-exhausted",
            evidence=evidence_bundle(),
        )

    assert captured.value.category == "timeout"
    assert captured.value.root_exception_class == "CropFindingOverallBudgetError"
    assert captured.value.effective_timeout_seconds == 0
    assert provider.generate_calls == 0
    assert provider.analysis_timeouts == []


@pytest.mark.parametrize(
    ("category", "status_code", "root_exception_class"),
    [
        ("rate_limit", 429, "RateLimitError"),
        ("server_error", 500, "InternalServerError"),
        ("server_error", 503, "InternalServerError"),
        ("connection", None, "APIConnectionError"),
    ],
)
@pytest.mark.asyncio
async def test_retryable_structured_analysis_failure_receives_at_most_one_retry(
    category,
    status_code,
    root_exception_class,
):
    failure = LLMProviderError(
        "safe transient failure",
        category=category,
        operation="structured_analysis",
        status_code=status_code,
        error_code="transient_code",
        provider_request_id="provider-request-analysis",
        root_exception_class=root_exception_class,
        retryable=True,
    )
    provider = ControlledProvider(generate_outcomes=[failure, {}])
    agent = CropFindingAgent(FakeTools({}), provider, request_id="request-analysis-retry")

    await agent._generate_payload(
        "prompt",
        {"type": "object"},
        action="DiscoverReferences",
        stage=2,
        request_id="request-analysis-retry",
        evidence=evidence_bundle(),
    )

    assert provider.generate_calls == 2
    assert provider.analysis_timeouts == [45, 45]


@pytest.mark.asyncio
async def test_invalid_request_structured_analysis_is_not_retried_and_preserves_metadata():
    failure = LLMProviderError(
        "OpenAI rejected the CropFinding request.",
        category="invalid_request",
        operation="structured_analysis",
        status_code=400,
        error_code="unsupported_parameter",
        provider_request_id="provider-request-400",
        root_exception_class="BadRequestError",
        retryable=False,
    )
    provider = ControlledProvider(generate_outcomes=[failure])
    agent = CropFindingAgent(FakeTools({}), provider, request_id="request-analysis-invalid")

    with pytest.raises(LLMProviderError) as captured:
        await agent._generate_payload(
            "prompt",
            {"type": "object"},
            action="DiscoverReferences",
            stage=2,
            request_id="request-analysis-invalid",
            evidence=evidence_bundle(),
        )

    error = captured.value
    assert error.stage == 2
    assert error.attempt == 1
    assert error.status_code == 400
    assert error.error_code == "unsupported_parameter"
    assert error.provider_request_id == "provider-request-400"
    assert provider.generate_calls == 1


@pytest.mark.asyncio
async def test_structured_analysis_classification_preserves_safe_provider_metadata():
    class FakeStatusError(Exception):
        def __init__(self):
            super().__init__("RAW_PROVIDER_SECRET")
            self.status_code = 503
            self.code = "server_is_overloaded"
            self.request_id = "provider-request-503"

    provider = ControlledProvider(generate_outcomes=[FakeStatusError()])
    tools = FakeTools({}, settings_overrides={"CROP_FINDING_RETRY_COUNT": 0})
    agent = CropFindingAgent(tools, provider, request_id="request-analysis-metadata")

    with pytest.raises(LLMProviderError) as captured:
        await agent._generate_payload(
            "prompt",
            {"type": "object"},
            action="DiscoverReferences",
            stage=1,
            request_id="request-analysis-metadata",
            evidence=evidence_bundle(),
        )

    error = captured.value
    assert error.category == "server_error"
    assert error.status_code == 503
    assert error.error_code == "server_is_overloaded"
    assert error.provider_request_id == "provider-request-503"
    assert error.root_exception_class == "FakeStatusError"
    assert "RAW_PROVIDER_SECRET" not in str(error)


@pytest.mark.asyncio
async def test_structured_analysis_diagnostics_are_safe_and_include_payload_metadata(caplog):
    caplog.set_level("WARNING", logger="agriassist.crop_finding")
    provider = ControlledProvider(generate_outcomes=[TimeoutError("DOCUMENT_SECRET")])
    agent = CropFindingAgent(FakeTools({}), provider, request_id="request-analysis-log")

    with pytest.raises(LLMProviderError):
        await agent._generate_payload(
            "PROMPT_SECRET",
            {"type": "object"},
            action="DiscoverReferences",
            stage=1,
            request_id="request-analysis-log",
            evidence=evidence_bundle(),
        )

    assert "configuredTimeoutSeconds=45" in caplog.text
    assert "effectiveTimeoutSeconds=45" in caplog.text
    assert "sourceCount=1" in caplog.text
    assert "chunkCount=1" in caplog.text
    assert "extractedCharacterCount=19" in caplog.text
    assert "PROMPT_SECRET" not in caplog.text
    assert "DOCUMENT_SECRET" not in caplog.text


def test_reference_evidence_bundle_deduplicates_and_keeps_relevant_neighbor_provenance():
    source = document(1, "local", "placeholder")
    source.segments = [
        ExtractedSegment(text="Unrelated opening content with no agricultural value.", section="Introduction"),
        ExtractedSegment(text="Context immediately before the supported statement.", section="Growth"),
        ExtractedSegment(text="MICH HY2 variety reaches maturity in 75 days.", section="Growth"),
        ExtractedSegment(text="Context immediately after the supported statement.", section="Growth"),
        ExtractedSegment(text="MICH HY2 variety reaches maturity in 75 days.", section="Growth"),
        ExtractedSegment(text="Unrelated ending content with no agricultural value.", section="Contacts"),
    ]
    agent = CropFindingAgent(FakeTools({}), ControlledProvider())

    bundle = agent._evidence_bundle(
        [source],
        focus_terms=["MICH HY2", "maturity", "growth stage"],
    )

    assert bundle.source_count == 1
    assert bundle.chunk_count == 3
    assert bundle.text.count("MICH HY2 variety reaches maturity in 75 days.") == 1
    assert "Context immediately before" in bundle.text
    assert "Context immediately after" in bundle.text
    assert "Unrelated opening" not in bundle.text
    assert "Unrelated ending" not in bundle.text
    assert "section=Growth" in bundle.text


def test_analysis_evidence_bundle_enforces_configurable_source_and_total_caps():
    source_a = document(1, "source-a", "A" * 3_000)
    source_b = document(1, "source-b", "B" * 3_000)
    tools = FakeTools(
        {},
        settings_overrides={
            "CROP_FINDING_ANALYSIS_MAX_CHARS_PER_SOURCE": 2_000,
            "CROP_FINDING_ANALYSIS_MAX_TOTAL_CHARS": 5_000,
        },
    )
    agent = CropFindingAgent(tools, ControlledProvider())

    bundle = agent._evidence_bundle([source_a, source_b])

    assert bundle.extracted_character_count == 4_000
    assert bundle.per_source_character_counts == {"source-a": 2_000, "source-b": 2_000}
