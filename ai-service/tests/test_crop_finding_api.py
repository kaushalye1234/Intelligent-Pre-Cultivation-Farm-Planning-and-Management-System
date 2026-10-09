import asyncio
from uuid import uuid4

from fastapi.testclient import TestClient

import main
from config import Settings, get_settings
from providers.base_llm_provider import LLMProviderError, ProviderConfigurationError
from schemas.crop_finding import CropSuggestion, CropSuggestionsResponse


def test_crop_finding_default_overall_timeout_is_175_seconds():
    assert Settings(_env_file=None).crop_finding_overall_timeout_seconds == 175


def test_crop_finding_endpoint_requires_service_token_and_validates_typed_input(monkeypatch):
    settings = Settings(
        _env_file=None,
        AI_SERVICE_TOKEN="test-service-token",
        AI_PROVIDER="openai",
        AI_MODEL="gpt-6-luna",
        OPENAI_API_KEY="test-openai-key",
    )
    main.app.dependency_overrides[get_settings] = lambda: settings

    async def fake_suggest(self, request):
        return CropSuggestionsResponse(
            requestId="request-1",
            usedInternationalFallback=False,
            sources=[],
            suggestions=[CropSuggestion(
                id="crop-1",
                name="Rice",
                evidenceStatus="Supported",
                explanation="Explicitly listed by an approved source.",
                provenance=[],
                warnings=[],
            )],
            analysis=[],
            recommendations=[],
            warnings=[],
        )

    monkeypatch.setattr(main.CropFindingAgent, "suggest_crops", fake_suggest)
    request = {"adminUserId": str(uuid4()), "maxSuggestions": 5}
    try:
        with TestClient(main.app) as client:
            assert client.post("/crop-finding/suggest-crops", json=request).status_code == 401
            headers = {"Authorization": "Bearer test-service-token"}
            response = client.post("/crop-finding/suggest-crops", json=request, headers=headers)
            assert response.status_code == 200
            assert response.json()["suggestions"][0]["name"] == "Rice"
            invalid = client.post(
                "/crop-finding/suggest-crops",
                json={**request, "maxSuggestions": 11},
                headers=headers,
            )
            assert invalid.status_code == 422
    finally:
        main.app.dependency_overrides.clear()


def test_crop_finding_provider_timeout_keeps_safe_504_and_logs_sanitized_context(monkeypatch, caplog):
    settings = Settings(
        _env_file=None,
        AI_SERVICE_TOKEN="test-service-token",
        AI_PROVIDER="openai",
        AI_MODEL="gpt-6-luna",
        OPENAI_API_KEY="SECRET_OPENAI_KEY",
    )
    main.app.dependency_overrides[get_settings] = lambda: settings
    caplog.set_level("WARNING", logger="agriassist.crop_finding")

    async def fake_discover(self, request):
        raise LLMProviderError(
            "OpenAI web search exceeded the configured CropFinding timeout.",
            category="timeout",
            operation="web_search",
            status_code=504,
            error_code="provider_timeout",
            provider_request_id="provider-request-504",
            root_exception_class="TimeoutError",
        ).add_context(
            request_id=self._request_id,
            action="DiscoverReferences",
            stage=1,
            attempt=1,
        ).add_diagnostics(
            configured_timeout_seconds=50,
            effective_timeout_seconds=50,
            elapsed_operation_ms=50_000,
            remaining_budget_seconds=125,
            source_count=0,
            chunk_count=0,
            extracted_character_count=0,
        )

    monkeypatch.setattr(main.CropFindingAgent, "discover_references", fake_discover)
    request = {
        "adminUserId": str(uuid4()),
        "cropTypeId": str(uuid4()),
        "cropName": "Chili",
        "region": "PROMPT_AND_DOCUMENT_SECRET",
    }
    try:
        with TestClient(main.app) as client:
            response = client.post(
                "/crop-finding/discover-references",
                json=request,
                headers={"Authorization": "Bearer test-service-token"},
            )

        assert response.status_code == 504
        assert response.json()["detail"] == {
            "code": "CROP_FINDING_TIMEOUT",
            "message": "OpenAI web search exceeded the configured CropFinding timeout.",
            "requestId": response.json()["detail"]["requestId"],
            "operation": "web_search",
            "stage": 1,
            "attempt": 1,
            "category": "timeout",
            "upstreamStatus": 504,
            "providerErrorCode": "provider_timeout",
            "providerRequestId": "provider-request-504",
            "configuredTimeoutSeconds": 50.0,
            "effectiveTimeoutSeconds": 50.0,
        }
        assert response.json()["detail"]["requestId"]
        assert "action=DiscoverReferences" in caplog.text
        assert "operation=web_search" in caplog.text
        assert "stage=1" in caplog.text
        assert "attempt=1" in caplog.text
        assert "category=timeout" in caplog.text
        assert "rootCauseClass=TimeoutError" in caplog.text
        assert "SECRET_OPENAI_KEY" not in caplog.text
        assert "test-service-token" not in caplog.text
        assert "PROMPT_AND_DOCUMENT_SECRET" not in caplog.text
    finally:
        main.app.dependency_overrides.clear()


def test_crop_finding_configuration_failure_returns_structured_503(monkeypatch):
    settings = Settings(
        _env_file=None,
        AI_SERVICE_TOKEN="test-service-token",
        AI_PROVIDER="openai",
        AI_MODEL="gpt-6-luna",
        OPENAI_API_KEY="test-openai-key",
    )
    main.app.dependency_overrides[get_settings] = lambda: settings

    async def fake_discover(self, request):
        self._active_operation = "web_search"
        self._active_stage = 1
        self._active_attempt = 1
        raise ProviderConfigurationError("RAW_CONFIGURATION_SECRET")

    monkeypatch.setattr(main.CropFindingAgent, "discover_references", fake_discover)
    request = {
        "adminUserId": str(uuid4()),
        "cropTypeId": str(uuid4()),
        "cropName": "Rice",
    }
    try:
        with TestClient(main.app) as client:
            response = client.post(
                "/crop-finding/discover-references",
                json=request,
                headers={"Authorization": "Bearer test-service-token"},
            )

        assert response.status_code == 503
        assert response.json()["detail"] == {
            "code": "CROP_FINDING_CONFIGURATION_UNAVAILABLE",
            "message": "CropFinding OpenAI configuration is unavailable.",
            "requestId": response.json()["detail"]["requestId"],
            "operation": "web_search",
            "stage": 1,
            "attempt": 1,
            "category": "configuration",
        }
        assert "RAW_CONFIGURATION_SECRET" not in response.text
    finally:
        main.app.dependency_overrides.clear()


def test_crop_finding_overall_timeout_returns_active_stage(monkeypatch):
    settings = Settings(
        _env_file=None,
        AI_SERVICE_TOKEN="test-service-token",
        AI_PROVIDER="openai",
        AI_MODEL="gpt-6-luna",
        OPENAI_API_KEY="test-openai-key",
        CROP_FINDING_OVERALL_TIMEOUT_SECONDS=175,
    )
    main.app.dependency_overrides[get_settings] = lambda: settings

    async def fake_discover(self, request):
        self._active_operation = "source_retrieval"
        self._active_stage = 2
        self._active_attempt = None
        raise asyncio.TimeoutError

    monkeypatch.setattr(main.CropFindingAgent, "discover_references", fake_discover)
    request = {
        "adminUserId": str(uuid4()),
        "cropTypeId": str(uuid4()),
        "cropName": "Rice",
    }
    try:
        with TestClient(main.app) as client:
            response = client.post(
                "/crop-finding/discover-references",
                json=request,
                headers={"Authorization": "Bearer test-service-token"},
            )

        assert response.status_code == 504
        assert response.json()["detail"] == {
            "code": "CROP_FINDING_TIMEOUT",
            "message": "CropFinding exceeded the 175-second operational deadline.",
            "requestId": response.json()["detail"]["requestId"],
            "operation": "source_retrieval",
            "stage": 2,
            "category": "timeout",
            "configuredTimeoutSeconds": 175.0,
            "effectiveTimeoutSeconds": 175.0,
        }
    finally:
        main.app.dependency_overrides.clear()
