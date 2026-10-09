import io
import json
from uuid import uuid4

import pytest
from PIL import Image

from agents.inspection_image_analysis_agent import InspectionImageAnalysisAgent, NoEvidenceAdapter
from agents.inspection_image_preprocessor import ImagePreprocessingError, InspectionImagePreprocessor
from providers.base_llm_provider import BaseLLMProvider, LLMProviderError, LLMResponse, StructuredGenerationRequest
from schemas.inspection_image_analysis import InspectionImageAnalysisInput
from tools.inspection_image_analysis_tools import VerifiedInspectionImage


def source_image(width: int = 2400, height: int = 1200) -> bytes:
    image = Image.new("RGB", (width, height), (87, 132, 62))
    exif = Image.Exif()
    exif[0x010E] = "private inspection note"
    output = io.BytesIO()
    image.save(output, format="JPEG", exif=exif)
    return output.getvalue()


def request() -> InspectionImageAnalysisInput:
    return InspectionImageAnalysisInput.model_validate({
        "contractVersion": 1,
        "analysisId": str(uuid4()),
        "imagePreprocessingVersion": 1,
        "cropName": "Rice",
        "varietyName": "Bg 352",
    })


PASS1 = {
    "contractVersion": 1,
    "visibleFindings": ["Several brown leaf spots are visible."],
    "possibleIssueCategories": ["Fungal", "DiseaseLike"],
    "severityIndicators": ["Multiple spots are present on the visible leaf."],
    "uncertainty": "One image is insufficient to determine the cause.",
    "requiresFurtherAssessment": True,
    "searchIntents": [{"issueCategory": "Fungal", "isPrimary": True, "terms": ["rice leaf spot symptoms"]}],
}

FINAL = {
    "contractVersion": 1,
    "visibleFindings": ["Several brown leaf spots are visible."],
    "possibleIssueCategory": "DiseaseLike",
    "possibleIssues": ["Possible fungal-type symptoms", "Other crop-health stress"],
    "severity": "Moderate",
    "uncertainty": "The cause cannot be determined from one image.",
    "validatedSourceReferences": [],
    "recommendedNonChemicalActions": ["InspectNearbyPlants", "MonitorSymptoms", "RequestFurtherAssessment"],
    "requiresFurtherAssessment": True,
    "groundingStatus": "Unavailable",
}


class FakeTools:
    def __init__(self, image_bytes: bytes) -> None:
        self.image_bytes = image_bytes
        self.calls = 0

    async def get_verified_image(self, analysis_id):
        self.calls += 1
        return VerifiedInspectionImage(analysis_id, uuid4(), "image/jpeg", "a" * 64, self.image_bytes)


class FakeProvider(BaseLLMProvider):
    provider_name = "fake"

    def __init__(self, fail_first: bool = False) -> None:
        self.fail_first = fail_first
        self.calls: list[StructuredGenerationRequest] = []

    async def generate_json(self, prompt: str, response_schema=None) -> LLMResponse:
        raise AssertionError("Legacy generation must not be used")

    async def generate_structured_json(self, structured_request: StructuredGenerationRequest) -> LLMResponse:
        self.calls.append(structured_request)
        if self.fail_first:
            raise LLMProviderError("failed", category="provider_failure")
        return LLMResponse(json.dumps(PASS1 if len(self.calls) == 1 else FINAL))


@pytest.mark.asyncio
async def test_pipeline_is_two_calls_and_uses_only_normalized_derivative():
    tools = FakeTools(source_image())
    provider = FakeProvider()

    result = await InspectionImageAnalysisAgent(tools, provider, NoEvidenceAdapter()).run(request())

    assert result.status == "Succeeded"
    assert tools.calls == 1
    assert len(provider.calls) == 2
    first, second = provider.calls
    assert first.image_bytes is not None and first.image_mime_type == "image/jpeg"
    assert second.image_bytes is None
    with Image.open(io.BytesIO(first.image_bytes)) as normalized:
        assert max(normalized.size) == 1600
        assert normalized.getexif() == {}


@pytest.mark.asyncio
async def test_provider_unavailable_stops_before_image_retrieval():
    tools = FakeTools(source_image())

    result = await InspectionImageAnalysisAgent(tools, None, NoEvidenceAdapter()).run(request())

    assert result.status == "Failed"
    assert result.failure_category == "provider_unavailable"
    assert tools.calls == 0


@pytest.mark.asyncio
async def test_pass1_failure_stops_before_second_model_call():
    provider = FakeProvider(fail_first=True)

    result = await InspectionImageAnalysisAgent(FakeTools(source_image()), provider, NoEvidenceAdapter()).run(request())

    assert result.status == "Failed"
    assert len(provider.calls) == 1
    assert result.pass1_result is None


def test_preprocessing_version_mismatch_rejects_before_decode():
    with pytest.raises(ImagePreprocessingError, match="version_mismatch"):
        InspectionImagePreprocessor.preprocess(b"not an image", "image/jpeg", 2)
