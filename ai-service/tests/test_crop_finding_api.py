from uuid import uuid4

from fastapi.testclient import TestClient

import main
from config import Settings, get_settings
from schemas.crop_finding import CropSuggestion, CropSuggestionsResponse


def test_crop_finding_endpoint_requires_service_token_and_validates_typed_input(monkeypatch):
    settings = Settings(
        _env_file=None,
        AI_SERVICE_TOKEN="test-service-token",
        OPENAI_API_KEY="test-openai-key",
        CROP_FINDING_MODEL="gpt-4.1-mini",
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
