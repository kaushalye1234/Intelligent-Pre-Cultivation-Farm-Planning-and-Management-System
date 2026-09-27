import json
from types import SimpleNamespace

import google.generativeai as genai
import pytest
from google.generativeai.types import generation_types

import providers.openai_provider as openai_provider_module
from providers.gemini_provider import GeminiProvider
from providers.openai_provider import OpenAIProvider
from schemas.field_analysis import CropFieldAnalysisOutput


RESPONSE_SCHEMA = {
    "type": "object",
    "properties": {"status": {"type": "string", "enum": ["Analyzed", "SafeFailure"]}},
    "required": ["status"],
    "additionalProperties": False,
}


@pytest.mark.asyncio
async def test_gemini_provider_passes_response_schema_with_json_mime_type(monkeypatch):
    captured = {}

    class FakeModel:
        def generate_content(self, prompt, generation_config):
            captured["prompt"] = prompt
            captured["generation_config"] = generation_config
            return SimpleNamespace(text='{"status":"Analyzed"}')

    monkeypatch.setattr(genai, "configure", lambda **kwargs: captured.update(configure=kwargs))
    monkeypatch.setattr(genai, "GenerativeModel", lambda model: captured.update(model=model) or FakeModel())
    provider = GeminiProvider(api_key="test-key", model="test-model", timeout_seconds=1)
    pydantic_schema = CropFieldAnalysisOutput.model_json_schema(by_alias=True)

    result = await provider.generate_json("field prompt", response_schema=pydantic_schema)

    assert result.text == '{"status":"Analyzed"}'
    assert captured["generation_config"]["response_mime_type"] == "application/json"
    gemini_schema = captured["generation_config"]["response_schema"]
    serialized = json.dumps(gemini_schema)
    assert "$defs" not in serialized
    assert "$ref" not in serialized
    assert "additionalProperties" not in serialized
    assert gemini_schema["properties"]["status"]["enum"] == ["Analyzed", "SafeFailure"]
    assert gemini_schema["properties"]["fieldCondition"]["required"] == ["summary"]
    normalized = generation_types.to_generation_config_dict(captured["generation_config"])
    assert normalized["response_schema"] is not None


@pytest.mark.asyncio
async def test_openai_provider_uses_json_schema_response_format(monkeypatch):
    captured = {}

    class FakeCompletions:
        async def create(self, **kwargs):
            captured.update(kwargs)
            return SimpleNamespace(
                choices=[SimpleNamespace(message=SimpleNamespace(content='{"status":"Analyzed"}'))]
            )

    fake_client = SimpleNamespace(chat=SimpleNamespace(completions=FakeCompletions()))
    monkeypatch.setattr(openai_provider_module, "AsyncOpenAI", lambda **kwargs: fake_client)
    provider = OpenAIProvider(api_key="test-key", model="test-model", timeout_seconds=1)

    result = await provider.generate_json("field prompt", response_schema=RESPONSE_SCHEMA)

    assert result.text == '{"status":"Analyzed"}'
    assert captured["response_format"] == {
        "type": "json_schema",
        "json_schema": {
            "name": "crop_field_analysis_output",
            "strict": False,
            "schema": RESPONSE_SCHEMA,
        },
    }


@pytest.mark.asyncio
async def test_openai_provider_preserves_json_object_mode_without_schema(monkeypatch):
    captured = {}

    class FakeCompletions:
        async def create(self, **kwargs):
            captured.update(kwargs)
            return SimpleNamespace(choices=[SimpleNamespace(message=SimpleNamespace(content="{}"))])

    fake_client = SimpleNamespace(chat=SimpleNamespace(completions=FakeCompletions()))
    monkeypatch.setattr(openai_provider_module, "AsyncOpenAI", lambda **kwargs: fake_client)
    provider = OpenAIProvider(api_key="test-key", model="test-model", timeout_seconds=1)

    await provider.generate_json("coordinator prompt")

    assert captured["response_format"] == {"type": "json_object"}
