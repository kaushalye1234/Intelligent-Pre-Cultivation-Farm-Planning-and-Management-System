from uuid import UUID

import pytest

from agents.weather_resource_agent import WeatherResourceAgent
from schemas.weather_resource import WeatherResourceInput

WORKFLOW_ID = UUID("11111111-1111-1111-1111-111111111111")
STEP_ID = UUID("22222222-2222-2222-2222-222222222222")
REQUEST_ID = UUID("33333333-3333-3333-3333-333333333333")
STOCK_ID = UUID("44444444-4444-4444-4444-444444444444")
RESOURCE_ID = UUID("55555555-5555-5555-5555-555555555555")


def weather_input(*, available=True, rain=2, temperature=31, wind=5, stock_quantity=20, threshold=8, stocks=True, requirements=None, member2_context=False):
    payload = {
        "workflowId": str(WORKFLOW_ID),
        "agentStepId": str(STEP_ID),
        "cropPlanRequestId": str(REQUEST_ID),
        "location": "Kurunegala",
        "preferredStartDate": "2026-10-01",
        "preferredEndDate": "2027-01-01",
        "fieldPriority": "Low",
        "fieldAnalysisSummary": "Stored field evidence is suitable for planning.",
        "weather": {
            "location": "Kurunegala",
            "isAvailable": available,
            "message": "provider unavailable" if not available else "forecast available",
            "days": [] if not available else [
                {
                    "date": "2026-09-15",
                    "minTemperatureC": 23,
                    "maxTemperatureC": temperature,
                    "rainMm": rain,
                    "maxWindSpeedMs": wind,
                    "description": "rain",
                }
            ],
        },
        "stocks": [] if not stocks else [
            {
                "inventoryStockId": str(STOCK_ID),
                "resourceId": str(RESOURCE_ID),
                "resourceName": "Paddy Seed",
                "unit": "kg",
                "quantityOnHand": stock_quantity + 4,
                "reservedQuantity": 4,
                "availableQuantity": stock_quantity,
                "lowStockThreshold": threshold,
            }
        ],
    }
    if requirements is not None:
        payload["resourceRequirements"] = requirements
    if member2_context:
        payload["member2FieldAnalysisContext"] = {
            "fieldSuitability": "SuitableWithConditions",
            "soilAssessment": "Soil type Loamy; condition Moderate; moisture Moist.",
            "waterAssessment": "Water availability Adequate; source Canal.",
            "drainageAssessment": "Drainage Poor; waterlogging risk Moderate.",
            "fieldPreparationRequirements": ["Clear drainage channels before planting."],
            "plantingReadiness": "RequiresPreparation",
            "identifiedRisks": ["PoorDrainage"],
            "recommendedPrePlantingActions": ["Address drainage before planting."],
            "priority": "Medium",
            "warnings": [],
            "requiresHumanReview": True,
        }
    return WeatherResourceInput.model_validate(payload)


@pytest.mark.asyncio
async def test_low_risk_preserves_inventory_snapshot_ids_and_values():
    result = await WeatherResourceAgent().run(weather_input())

    assert result.status == "Analyzed"
    assert result.weather_risk == "Low"
    assert result.resource_checks[0].inventory_stock_id == STOCK_ID
    assert result.resource_checks[0].available_quantity == 20
    assert result.resource_checks[0].is_low_stock is False


@pytest.mark.asyncio
async def test_safe_member2_context_is_read_only_and_does_not_change_weather_inventory_reasoning():
    request = weather_input(member2_context=True)

    result = await WeatherResourceAgent().run(request)

    assert request.member_2_field_analysis_context is not None
    assert request.member_2_field_analysis_context.water_assessment.startswith("Water availability Adequate")
    assert request.member_2_field_analysis_context.drainage_assessment.startswith("Drainage Poor")
    assert request.member_2_field_analysis_context.planting_readiness == "RequiresPreparation"
    assert request.member_2_field_analysis_context.identified_risks == ["PoorDrainage"]
    assert result.weather_risk == "Low"
    assert result.resource_checks[0].available_quantity == 20


@pytest.mark.asyncio
async def test_high_weather_threshold_requires_human_review():
    result = await WeatherResourceAgent().run(weather_input(rain=30))

    assert result.weather_risk == "High"
    assert result.requires_human_review is True
    assert "agricultural officer" in result.recommendations[0]


@pytest.mark.asyncio
async def test_medium_weather_threshold_is_deterministic():
    result = await WeatherResourceAgent().run(weather_input(temperature=34))

    assert result.weather_risk == "Medium"
    assert "maximum temperature 34 C" in result.weather_summary


@pytest.mark.asyncio
async def test_missing_forecast_returns_unknown_without_inventing_weather():
    result = await WeatherResourceAgent().run(weather_input(available=False))

    assert result.weather_risk == "Unknown"
    assert result.requires_human_review is True
    assert "could not be calculated" in result.warnings[0]


@pytest.mark.asyncio
async def test_low_stock_is_flagged_without_reserving_inventory():
    result = await WeatherResourceAgent().run(weather_input(stock_quantity=6, threshold=8))

    assert result.resource_checks[0].is_low_stock is True
    assert result.resource_checks[0].available_quantity == 6
    assert result.recommendations[-1] == "Restock Paddy Seed: only 6 kg available."


@pytest.mark.asyncio
async def test_missing_inventory_snapshot_requires_review():
    result = await WeatherResourceAgent().run(weather_input(stocks=False))

    assert result.requires_human_review is True
    assert result.resource_checks == []
    assert "No active inventory rows" in result.warnings[0]


@pytest.mark.asyncio
async def test_missing_requirement_is_unknown_and_never_invented():
    result = await WeatherResourceAgent().run(weather_input())

    check = result.resource_checks[0]
    assert check.requested is None
    assert check.sufficient is None
    assert check.requirement_status == "ResourceRequirementUnknown"
    assert any("ResourceRequirementUnknown" in warning for warning in result.warnings)
    dumped = result.model_dump(by_alias=True)["resourceChecks"][0]
    assert dumped["requested"] is None and dumped["sufficient"] is None
    assert dumped["requirementStatus"] == "ResourceRequirementUnknown"


@pytest.mark.asyncio
async def test_sufficient_requirement_reports_requested_quantity():
    result = await WeatherResourceAgent().run(
        weather_input(stock_quantity=20, requirements=[{"resourceId": str(RESOURCE_ID), "requestedQuantity": 15}])
    )

    check = result.resource_checks[0]
    assert (check.requested, check.sufficient, check.requirement_status) == (15, True, "Sufficient")
    assert not any("ResourceRequirementUnknown" in warning for warning in result.warnings)
    assert result.requires_human_review is False


@pytest.mark.asyncio
async def test_insufficient_requirement_requires_human_review():
    result = await WeatherResourceAgent().run(
        weather_input(stock_quantity=20, requirements=[{"resourceId": str(RESOURCE_ID), "requestedQuantity": 25}])
    )

    check = result.resource_checks[0]
    assert (check.requested, check.sufficient, check.requirement_status) == (25, False, "Insufficient")
    assert result.requires_human_review is True
    assert any("20 kg available, 25 kg requested" in warning for warning in result.warnings)


@pytest.mark.asyncio
async def test_requirement_for_unstocked_resource_is_flagged_not_invented():
    other = "99999999-9999-9999-9999-999999999999"
    result = await WeatherResourceAgent().run(
        weather_input(requirements=[{"resourceId": other, "requestedQuantity": 3}])
    )

    assert result.requires_human_review is True
    assert any(other in warning for warning in result.warnings)
    assert result.resource_checks[0].requirement_status == "ResourceRequirementUnknown"
