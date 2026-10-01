import asyncio
from types import SimpleNamespace

import pytest
from pydantic import ValidationError

import providers.openai_provider as openai_provider_module
from config import Settings
from providers import create_crop_finding_provider, create_provider
from providers.base_llm_provider import LLMProviderError
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
    assert not hasattr(settings, "crop_finding_model")
    assert provider._web_search_timeout_seconds == 50
    assert provider._web_search_client.max_retries == 0
    assert settings.crop_finding_structured_analysis_timeout_seconds == 45
    assert settings.crop_finding_completion_safety_margin_seconds == 5
    assert settings.crop_finding_analysis_max_chars_per_source == 12_000
    assert settings.crop_finding_analysis_max_total_chars == 45_000

    shared_provider = create_provider(settings)
    assert shared_provider is not None
    assert shared_provider._web_search_client is shared_provider._client
    assert shared_provider._timeout_seconds == settings.provider_timeout_seconds


def test_crop_finding_web_search_timeout_must_be_shorter_than_overall_timeout():
    with pytest.raises(ValidationError):
        Settings(
            _env_file=None,
            AI_PROVIDER="openai",
            AI_MODEL="gpt-6-luna",
            OPENAI_API_KEY="test-key",
            CROP_FINDING_WEB_SEARCH_TIMEOUT_SECONDS=105,
            CROP_FINDING_OVERALL_TIMEOUT_SECONDS=105,
        )


def test_crop_finding_analysis_timeout_and_margin_must_fit_overall_timeout():
    with pytest.raises(ValidationError):
        Settings(
            _env_file=None,
            AI_PROVIDER="openai",
            AI_MODEL="gpt-6-luna",
            OPENAI_API_KEY="test-key",
            CROP_FINDING_STRUCTURED_ANALYSIS_TIMEOUT_SECONDS=101,
            CROP_FINDING_COMPLETION_SAFETY_MARGIN_SECONDS=5,
            CROP_FINDING_OVERALL_TIMEOUT_SECONDS=105,
        )


def test_crop_finding_analysis_character_caps_cannot_exceed_extraction_caps():
    with pytest.raises(ValidationError):
        Settings(
            _env_file=None,
            AI_PROVIDER="openai",
            AI_MODEL="gpt-6-luna",
            OPENAI_API_KEY="test-key",
            CROP_FINDING_MAX_EXTRACTED_CHARS_PER_SOURCE=10_000,
            CROP_FINDING_ANALYSIS_MAX_CHARS_PER_SOURCE=12_000,
        )


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
async def test_crop_finding_analysis_uses_dynamic_timeout_and_disables_sdk_retries(monkeypatch):
    captured = {"with_options": []}

    class FakeCompletions:
        async def create(self, **kwargs):
            captured["request"] = kwargs
            return SimpleNamespace(
                choices=[SimpleNamespace(message=SimpleNamespace(content='{"status":"Analyzed"}'))]
            )

    class FakeClient:
        def __init__(self):
            self.chat = SimpleNamespace(completions=FakeCompletions())

        def with_options(self, **kwargs):
            captured["with_options"].append(kwargs)
            return self

    fake_client = FakeClient()
    monkeypatch.setattr(openai_provider_module, "AsyncOpenAI", lambda **kwargs: fake_client)
    provider = OpenAIProvider(api_key="test-key", model="gpt-6-luna", timeout_seconds=30)

    result = await provider.generate_crop_finding_json(
        "crop finding prompt",
        response_schema=RESPONSE_SCHEMA,
        timeout_seconds=42.5,
    )

    assert result.text == '{"status":"Analyzed"}'
    assert captured["with_options"] == [{"timeout": 42.5, "max_retries": 0}]
    assert captured["request"]["response_format"]["json_schema"]["name"] == "crop_finding_output"
    assert provider._timeout_seconds == 30


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


@pytest.mark.asyncio
async def test_openai_web_search_timeout_has_stable_safe_classification():
    class SlowResponses:
        async def create(self, **kwargs):
            await asyncio.sleep(0.05)

    provider = OpenAIProvider(
        api_key="test-key",
        model="gpt-6-luna",
        timeout_seconds=1,
        web_search_timeout_seconds=0.01,
        web_search_max_retries=0,
    )
    provider._web_search_client = SimpleNamespace(responses=SlowResponses())

    with pytest.raises(LLMProviderError) as captured:
        await provider.search_web("PROMPT_SECRET", ["doa.gov.lk"], 5)

    error = captured.value
    assert error.category == "timeout"
    assert error.retryable is False
    assert str(error) == "OpenAI web search exceeded the configured CropFinding timeout."
    assert "PROMPT_SECRET" not in str(error)


@pytest.mark.parametrize(
    ("status_code", "expected_category", "retryable"),
    [(429, "rate_limit", True), (400, "invalid_request", False), (500, "server_error", True), (503, "server_error", True)],
)
@pytest.mark.asyncio
async def test_openai_web_search_preserves_safe_status_metadata(status_code, expected_category, retryable):
    class FakeStatusError(Exception):
        def __init__(self):
            super().__init__("raw response body with SECRET_TOKEN")
            self.status_code = status_code
            self.code = "test_code"
            self.request_id = "provider-request-1"

    class FailingResponses:
        async def create(self, **kwargs):
            raise FakeStatusError()

    provider = OpenAIProvider(api_key="test-key", model="gpt-6-luna", timeout_seconds=1)
    provider._web_search_client = SimpleNamespace(responses=FailingResponses())

    with pytest.raises(LLMProviderError) as captured:
        await provider.search_web("PROMPT_SECRET", ["doa.gov.lk"], 5)

    error = captured.value
    assert error.category == expected_category
    assert error.retryable is retryable
    assert error.status_code == status_code
    assert error.error_code == "test_code"
    assert error.provider_request_id == "provider-request-1"
    assert error.root_exception_class == "FakeStatusError"
    assert "SECRET_TOKEN" not in str(error)
    assert "PROMPT_SECRET" not in str(error)


@pytest.mark.asyncio
async def test_openai_web_search_classifies_transient_connection_failure():
    class FailingResponses:
        async def create(self, **kwargs):
            raise ConnectionError("SECRET_CONNECTION_DETAIL")

    provider = OpenAIProvider(api_key="test-key", model="gpt-6-luna", timeout_seconds=1)
    provider._web_search_client = SimpleNamespace(responses=FailingResponses())

    with pytest.raises(LLMProviderError) as captured:
        await provider.search_web("PROMPT_SECRET", ["doa.gov.lk"], 5)

    assert captured.value.category == "connection"
    assert captured.value.retryable is True
    assert str(captured.value) == "OpenAI connection failed."
