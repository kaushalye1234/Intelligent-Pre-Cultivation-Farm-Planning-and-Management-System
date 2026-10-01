from types import SimpleNamespace

import pytest

import providers.openai_provider as openai_provider_module
from config import Settings
from providers import create_crop_finding_provider
from providers.openai_provider import OpenAIProvider


RESPONSE_SCHEMA = {
    "type": "object",
    "properties": {"status": {"type": "string", "enum": ["Analyzed", "SafeFailure"]}},
    "required": ["status"],
    "additionalProperties": False,
}


def test_crop_finding_provider_uses_shared_openai_model_configuration():
    settings = Settings(
        _env_file=None,
        AI_PROVIDER="openai",
        AI_MODEL="gpt-6-luna",
        OPENAI_API_KEY="test-key",
    )

    provider = create_crop_finding_provider(settings)

    assert provider is not None
    assert provider._model == settings.ai_model


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


@pytest.mark.asyncio
async def test_openai_provider_web_search_uses_domain_filters_and_returns_sources(monkeypatch):
    captured = {}

    class FakeResponses:
        async def create(self, **kwargs):
            captured.update(kwargs)
            return SimpleNamespace(model_dump=lambda **_: {
                "output": [{"type": "web_search_call", "action": {"sources": [
                    {"url": "https://doa.gov.lk/hordi-home/", "title": "HORDI"},
                    {"url": "https://doa.gov.lk/hordi-home/", "title": "duplicate"},
                ]}}]
            })

    fake_client = SimpleNamespace(
        chat=SimpleNamespace(completions=SimpleNamespace()),
        responses=FakeResponses(),
    )
    monkeypatch.setattr(openai_provider_module, "AsyncOpenAI", lambda **kwargs: fake_client)
    provider = OpenAIProvider(api_key="test-key", model="gpt-6-luna", timeout_seconds=1)

    result = await provider.search_web("Find crops", ["doa.gov.lk"], 5)

    assert [source.url for source in result.sources] == ["https://doa.gov.lk/hordi-home/"]
    assert captured["tools"][0]["filters"]["allowed_domains"] == ["doa.gov.lk"]
    assert captured["include"] == ["web_search_call.action.sources"]
    assert captured["store"] is False
