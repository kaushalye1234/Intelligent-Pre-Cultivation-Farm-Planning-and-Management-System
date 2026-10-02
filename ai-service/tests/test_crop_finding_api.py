from uuid import uuid4

from fastapi.testclient import TestClient

import main
from config import Settings, get_settings
from providers.base_llm_provider import LLMProviderError
from schemas.crop_finding import CropSuggestion, CropSuggestionsResponse


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


def test_crop_finding_provider_failure_keeps_safe_502_and_logs_sanitized_context(monkeypatch, caplog):
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
            root_exception_class="TimeoutError",
        ).add_context(
            request_id=self._request_id,
            action="DiscoverReferences",
            stage=1,
            attempt=1,
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

        assert response.status_code == 502
        assert response.json()["detail"] == "CropFinding source discovery or analysis failed."
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
