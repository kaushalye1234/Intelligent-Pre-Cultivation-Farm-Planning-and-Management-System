import httpx
import pytest

import main
from config import Settings, get_settings
from main import app
from providers.base_llm_provider import LLMResponse


class FakeProvider:
    async def generate_json(self, prompt, response_schema=None):
        return LLMResponse(text='''{
          "contractVersion": 1,
          "workflowId": "11111111-1111-1111-1111-111111111111",
          "approvedRevision": 2,
          "weeklyGuidance": ["Check the field for standing water."],
          "currentStageExplanation": "The crop is in an early growth stage.",
          "monthlyGuidance": [],
          "risks": [],
          "harvestPreparation": [],
          "whyThisPlan": "This guide uses approved crop and field information."
        }''')


@pytest.mark.asyncio
async def test_final_guide_route_requires_internal_service_token(monkeypatch):
    app.dependency_overrides[get_settings] = lambda: Settings(AI_SERVICE_TOKEN="test-only-token")
    monkeypatch.setattr(main, "create_provider", lambda settings: FakeProvider())
    try:
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://testserver") as client:
            response = await client.post(
                "/workflows/crop-planning/final-cultivation-guide",
                headers={"X-AgriAssist-AI-Token": "test-only-token"},
                json={
                    "contractVersion": 1,
                    "workflowId": "11111111-1111-1111-1111-111111111111",
                    "approvedRevision": 2,
                    "cropPlanRequestId": "22222222-2222-2222-2222-222222222222",
                    "cropName": "Maize",
                    "preferredStartDate": "2026-10-20",
                    "preferredEndDate": "2027-02-15",
                    "currentDate": "2026-10-20",
                    "evidenceSummary": [],
                    "approvedActivities": [],
                },
            )
    finally:
        app.dependency_overrides.clear()

    assert response.status_code == 200, response.text
    result = response.json()
    assert result["workflowId"] == "11111111-1111-1111-1111-111111111111"
    assert result["approvedRevision"] == 2
    assert "approvedActivities" not in result
