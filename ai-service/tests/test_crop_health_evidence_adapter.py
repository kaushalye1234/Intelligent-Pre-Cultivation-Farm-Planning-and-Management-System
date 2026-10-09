from datetime import datetime, timezone
from uuid import uuid4

import pytest

from config import Settings
from schemas.inspection_image_analysis import InspectionImageAnalysisInput, InspectionImagePass1Result
from tools.crop_finding_tools import ExtractedSegment, RetrievedDocument, SourcePolicy, SourcePolicyEntry
from tools.crop_health_evidence_adapter import CropHealthEvidenceAdapter


def policy_entry(source_id: str, host: str, stage: int) -> SourcePolicyEntry:
    return SourcePolicyEntry(
        id=source_id,
        organization_name=f"Organization {source_id}",
        approved_hosts=(host,),
        path_prefixes=("/",),
        country="Sri Lanka" if stage == 1 else "International",
        source_category="Research institute",
        stage=stage,
        relevance=("rice", "plant protection", "crop health"),
        rationale="Trusted crop guidance.",
    )


def document(source_id: str, host: str, stage: int, *, strong: bool = True, crop: bool = True, issue: bool = True) -> RetrievedDocument:
    crop_text = "rice paddy" if crop else "general agriculture"
    issue_text = "fungal leaf spot symptoms" if issue else "cultivation calendar"
    heading = f"{crop_text} {issue_text}" if strong else "Extension information"
    body = (
        f"{crop_text} {issue_text}. Inspect nearby plants and monitor symptoms. Complete field sanitation and "
        "remove affected crop residue where appropriate. Further assessment by an extension officer is advised. "
    ) * 5
    return RetrievedDocument(
        source_id=f"{source_id}:doc",
        title=heading if strong else f"{crop_text} {issue_text}",
        organization_name=f"Organization {source_id}",
        original_url=f"https://{host}/",
        final_url=f"https://{host}/guide",
        source_category="Research institute",
        country="Sri Lanka" if stage == 1 else "International",
        stage=stage,
        content_type="text/html",
        retrieval_status="Retrieved",
        retrieved_at=datetime.now(timezone.utc).isoformat(),
        acceptance_reason="Trusted.",
        segments=[ExtractedSegment(text=body, section=heading)],
    )


class FakeTools:
    def __init__(self, documents: dict[str, RetrievedDocument]) -> None:
        self.documents = documents
        self.calls: list[tuple[str, int]] = []

    async def retrieve(self, url: str, title: str, stage: int) -> RetrievedDocument:
        self.calls.append((url, stage))
        return self.documents[url]

    @staticmethod
    def normalize_url(url: str) -> str:
        return url.rstrip("/") or url


def settings(**overrides) -> Settings:
    values = {
        "AI_PROVIDER": "openai",
        "AI_MODEL": "gpt-6-luna",
        "OPENAI_API_KEY": "test-key",
        "INSPECTION_IMAGE_RETRIEVAL_TIMEOUT_SECONDS": 35,
        **overrides,
    }
    return Settings(_env_file=None, **values)


def request() -> InspectionImageAnalysisInput:
    return InspectionImageAnalysisInput.model_validate({
        "contractVersion": 1,
        "analysisId": str(uuid4()),
        "imagePreprocessingVersion": 1,
        "cropName": "Rice",
        "varietyName": None,
    })


def pass1() -> InspectionImagePass1Result:
    return InspectionImagePass1Result.model_validate({
        "contractVersion": 1,
        "visibleFindings": ["Brown leaf spots are visible."],
        "possibleIssueCategories": ["Fungal"],
        "severityIndicators": [],
        "uncertainty": "The cause is uncertain.",
        "requiresFurtherAssessment": True,
        "searchIntents": [{"issueCategory": "Fungal", "isPrimary": True, "terms": ["fungal symptoms"]}],
    })


@pytest.mark.asyncio
async def test_one_strong_stage1_document_stops_and_suppresses_stage2():
    stage1 = policy_entry("lk-rice", "lk.example", 1)
    stage2 = policy_entry("intl-rice", "intl.example", 2)
    tools = FakeTools({"https://lk.example/": document(stage1.id, "lk.example", 1)})
    adapter = CropHealthEvidenceAdapter(settings(), tools=tools, source_policy=SourcePolicy([stage1, stage2]))

    evidence = await adapter.retrieve(request(), pass1())

    assert len(evidence) == 1
    assert evidence[0].source_stage == "Stage1"
    assert evidence[0].relevance_signals.strong_evidence is True
    assert tools.calls == [("https://lk.example/", 1)]


@pytest.mark.asyncio
async def test_zero_usable_stage1_documents_triggers_stage2():
    stage1 = policy_entry("lk-generic", "lk.example", 1)
    stage2 = policy_entry("intl-rice", "intl.example", 2)
    tools = FakeTools({
        "https://lk.example/": document(stage1.id, "lk.example", 1, crop=False),
        "https://intl.example/": document(stage2.id, "intl.example", 2),
    })
    adapter = CropHealthEvidenceAdapter(settings(), tools=tools, source_policy=SourcePolicy([stage1, stage2]))

    evidence = await adapter.retrieve(request(), pass1())

    assert [item.source_stage for item in evidence] == ["Stage2"]
    assert [stage for _, stage in tools.calls] == [1, 2]


@pytest.mark.asyncio
async def test_crop_match_without_issue_match_is_not_usable():
    stage1 = policy_entry("lk-rice", "lk.example", 1)
    tools = FakeTools({"https://lk.example/": document(stage1.id, "lk.example", 1, issue=False)})
    adapter = CropHealthEvidenceAdapter(settings(), tools=tools, source_policy=SourcePolicy([stage1]))

    assert await adapter.retrieve(request(), pass1()) == []


@pytest.mark.asyncio
async def test_duplicate_snippets_are_not_sent_twice():
    first = policy_entry("lk-one", "one.example", 1)
    second = policy_entry("lk-two", "two.example", 1)
    tools = FakeTools({
        "https://one.example/": document(first.id, "one.example", 1, strong=False),
        "https://two.example/": document(second.id, "two.example", 1, strong=False),
    })
    adapter = CropHealthEvidenceAdapter(
        settings(INSPECTION_IMAGE_STRONG_EVIDENCE_THRESHOLD=100),
        tools=tools,
        source_policy=SourcePolicy([first, second]),
    )

    evidence = await adapter.retrieve(request(), pass1())

    assert len(evidence) == 1


def test_same_document_inputs_produce_same_relevance_result():
    entry = policy_entry("lk-rice", "lk.example", 1)
    adapter = CropHealthEvidenceAdapter(settings(), tools=FakeTools({}), source_policy=SourcePolicy([entry]))
    doc = document(entry.id, "lk.example", 1)
    issue_terms, primary_terms = adapter._issue_terms(pass1())

    first = adapter._score_document(doc, ("rice", "paddy"), issue_terms, primary_terms)
    second = adapter._score_document(doc, ("rice", "paddy"), issue_terms, primary_terms)

    assert first is not None and second is not None
    assert first.evidence.relevance_signals == second.evidence.relevance_signals
    assert first.evidence.exact_extract == second.evidence.exact_extract


def test_source_policy_hash_uses_the_exact_shared_policy_content():
    policy = SourcePolicy.load_default()

    assert policy.version == "2026-10-01"
    assert len(policy.content_hash) == 64
