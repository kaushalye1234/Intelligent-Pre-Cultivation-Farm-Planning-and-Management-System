import httpx
import pytest

from config import Settings, get_settings
from main import app
from test_scheduling_validation_agent import sourced_request


@pytest.mark.asyncio
@pytest.mark.parametrize("weather_risk,expected_status,approvable", [
    ("Medium", "CandidateReady", True),
    ("High", "CandidateBlocked", False),
])
async def test_scheduling_http_contract_preserves_review_and_source_evidence(
    weather_risk, expected_status, approvable
):
    request = sourced_request()
    request.weather_resource_output["weatherRisk"] = weather_risk
    app.dependency_overrides[get_settings] = lambda: Settings(AI_SERVICE_TOKEN="test-only-token")
    try:
        async with httpx.AsyncClient(
            transport=httpx.ASGITransport(app=app), base_url="http://testserver"
        ) as client:
            response = await client.post(
                "/workflows/crop-planning/scheduling-validation",
                headers={"X-AgriAssist-AI-Token": "test-only-token"},
                json=request.model_dump(by_alias=True, mode="json"),
            )
    finally:
        app.dependency_overrides.clear()

    assert response.status_code == 200, response.text
    result = response.json()
    assert result["contractVersion"] == 2
    assert result["status"] == expected_status
    assert result["requiresHumanApproval"] is approvable
    assert len(result["candidateTasks"]) == 3
    assert result["candidateIrrigation"] == []
    assert result["candidateTasks"][0]["sources"][0]["kind"] == "FieldAnalysis"
    if approvable:
        assert result["candidateReservations"][0]["sources"][0]["kind"] == "ResourceRequirement"
    else:
        assert result["candidateReservations"][0]["sources"][0]["kind"] == "ResourceRequirement"
