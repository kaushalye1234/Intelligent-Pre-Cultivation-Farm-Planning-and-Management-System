import json

import pytest

from agents.inspection_note_assistant_agent import InspectionNoteAssistantAgent
from providers.base_llm_provider import BaseLLMProvider, LLMResponse, StructuredGenerationRequest
from schemas.inspection_note_assistance import InspectionNoteAssistanceInput


def note_input() -> InspectionNoteAssistanceInput:
    return InspectionNoteAssistanceInput.model_validate({
        "contractVersion": 1,
        "cropName": "Rice",
        "varietyName": "Bg 352",
        "fieldName": "North paddy",
        "fieldSoilType": "Loam",
        "draft": {
            "soilCondition": "Moderate",
            "waterAvailability": "Limited",
            "drainageCondition": "Poor",
            "identifiedRisks": ["Waterlogging"],
        },
    })


class FakeProvider(BaseLLMProvider):
    provider_name = "fake"

    def __init__(self, payload: dict) -> None:
        self.payload = payload
        self.calls: list[StructuredGenerationRequest] = []

    async def generate_json(self, prompt: str, response_schema=None) -> LLMResponse:
        raise AssertionError("Legacy generation must not be used")

    async def generate_structured_json(self, request: StructuredGenerationRequest) -> LLMResponse:
        self.calls.append(request)
        return LLMResponse(json.dumps(self.payload))


@pytest.mark.asyncio
async def test_note_assistant_uses_one_text_only_strict_call():
    provider = FakeProvider({
        "contractVersion": 1,
        "status": "Available",
        "suggestions": {
            "soilNotes": "Soil condition was recorded as moderate.",
            "waterConcerns": "Water availability was recorded as limited.",
            "drainageNotes": "Drainage condition was recorded as poor.",
            "generalFieldNotes": None,
            "riskNotes": "Waterlogging was selected as a risk.",
            "officerNotes": None,
        },
        "contradictionWarnings": [],
        "missingDataWarnings": ["Soil moisture was not supplied."],
        "failureCategory": None,
    })

    result = await InspectionNoteAssistantAgent(provider).run(note_input())

    assert result.status == "Available"
    assert result.suggestions is not None
    assert result.suggestions.drainage_notes == "Drainage condition was recorded as poor."
    assert len(provider.calls) == 1
    assert provider.calls[0].image_bytes is None
    assert provider.calls[0].response_schema["additionalProperties"] is False


@pytest.mark.asyncio
async def test_note_assistant_is_non_blocking_when_provider_is_unavailable():
    result = await InspectionNoteAssistantAgent(None).run(note_input())

    assert result.status == "Unavailable"
    assert result.suggestions is None
    assert result.failure_category == "provider_unavailable"


@pytest.mark.asyncio
async def test_note_assistant_rejects_unknown_output_fields():
    provider = FakeProvider({
        "contractVersion": 1,
        "status": "Available",
        "suggestions": {
            "soilNotes": None,
            "waterConcerns": None,
            "drainageNotes": None,
            "generalFieldNotes": None,
            "riskNotes": None,
            "officerNotes": None,
            "inventedField": "not allowed",
        },
        "contradictionWarnings": [],
        "missingDataWarnings": [],
        "failureCategory": None,
    })

    result = await InspectionNoteAssistantAgent(provider).run(note_input())

    assert result.status == "Unavailable"
    assert result.failure_category == "schema_mismatch"
