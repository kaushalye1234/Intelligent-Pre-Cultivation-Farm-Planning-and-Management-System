import json
from uuid import UUID

import pytest

from agents.weather_resource_agent import WeatherResourceAgent
from providers.base_llm_provider import LLMProviderError, LLMResponse
from schemas.weather_resource import (
    CropResourceRequirements,
    ReservationSnapshot,
    StockSnapshot,
    WeatherForecast,
    WeatherResourceInput,
)
from tools.backend_tool_client import ToolClientError

WORKFLOW_ID = UUID("11111111-1111-1111-1111-111111111111")
STEP_ID = UUID("22222222-2222-2222-2222-222222222222")
REQUEST_ID = UUID("33333333-3333-3333-3333-333333333333")
STOCK_ID = UUID("44444444-4444-4444-4444-444444444444")
UREA_ID = UUID("55555555-5555-5555-5555-555555555555")
FIELD_ID = UUID("66666666-6666-6666-6666-666666666666")
RULE_ID = UUID("77777777-7777-7777-7777-777777777777")
PROFILE_ID = UUID("88888888-8888-8888-8888-888888888888")
CROP_ID = UUID("99999999-9999-9999-9999-999999999999")

# SAMPLE TEST DATA ONLY - not an agronomic recommendation. It mirrors the worked example
# "Tomato -> Urea -> 100 kg/acre" so the arithmetic 100 x 0.5 acre = 50 kg can be checked.
SAMPLE_RATE_KG_PER_ACRE = 100
SAMPLE_FIELD_AREA_ACRE = 0.5


def request_input(field_priority="Low") -> WeatherResourceInput:
    return WeatherResourceInput.model_validate({
        "workflowId": str(WORKFLOW_ID),
        "agentStepId": str(STEP_ID),
        "cropPlanRequestId": str(REQUEST_ID),
        "fieldId": str(FIELD_ID),
        "location": "Kurunegala",
        "preferredStartDate": "2026-10-01",
        "preferredEndDate": "2027-01-01",
        "fieldPriority": field_priority,
        "fieldAnalysisSummary": "Stored field evidence is suitable for planning.",
    })


def requirements_result(*, verified=True, field_area=SAMPLE_FIELD_AREA_ACRE, match="Matched", unit="kg") -> dict:
    base = {
        "cropPlanRequestId": str(REQUEST_ID),
        "cropTypeId": str(CROP_ID),
        "cropName": "Tomato",
        "fieldId": str(FIELD_ID),
        "fieldArea": field_area,
        "fieldAreaUnit": "acre",
    }
    if not verified:
        return {**base, "status": "Unavailable", "reason": "No verified crop-resource requirement is available for Tomato.", "requirements": []}
    rule = {
        "ruleId": str(RULE_ID),
        "ruleKey": "Urea",
        "resourceId": str(UREA_ID) if match == "Matched" else None,
        "resourceName": "Urea",
        "resourceMatch": match,
        "quantityPerArea": SAMPLE_RATE_KG_PER_ACRE,
        "resourceUnit": unit,
        "areaUnit": "acre",
    }
    source = {
        "cropReferenceProfileId": str(PROFILE_ID), "sourceName": "SAMPLE source (test)", "sourceVersion": "test",
        "verifiedAt": "2026-01-01T00:00:00Z",
    }
    if field_area is None:
        rule |= {"requiredQuantity": None, "status": "Unknown", "reason": "The field area is not recorded."}
        return {**base, "status": "Incomplete", "reason": "The field area is not recorded.", "source": source, "requirements": [rule]}
    required = SAMPLE_RATE_KG_PER_ACRE * field_area
    rule |= {"requiredQuantity": required, "status": "Calculated", "basis": f"100 {unit}/acre x {field_area:g} acre = {required:g} {unit}"}
    return {**base, "status": "Available", "source": source, "requirements": [rule]}


def stock(available=30.0, reserved=0.0, threshold=5.0, unit="kg") -> dict:
    return {
        "inventoryStockId": str(STOCK_ID), "resourceId": str(UREA_ID), "resourceName": "Urea", "unit": unit,
        "quantityOnHand": available + reserved, "reservedQuantity": reserved, "availableQuantity": available,
        "lowStockThreshold": threshold,
    }


def forecast(*, available=True, rain=2, temperature=31, wind=5) -> dict:
    return {
        "location": "Kurunegala",
        "isAvailable": available,
        "message": "forecast available" if available else "provider unavailable",
        "days": [] if not available else [{
            "date": "2026-09-15", "minTemperatureC": 23, "maxTemperatureC": temperature,
            "rainMm": rain, "maxWindSpeedMs": wind, "description": "rain",
        }],
    }


class FakeTools:
    """Stands in for the backend tools and records every call so tests can prove no write tool exists."""

    def __init__(self, *, requirements=None, stocks=None, reservations=None, weather=None, fail=()) -> None:
        self.requirements = requirements if requirements is not None else requirements_result()
        self.stocks = stocks if stocks is not None else [stock()]
        self.reservations = reservations or []
        self.weather = weather if weather is not None else forecast()
        self.fail = set(fail)
        self.calls: list[tuple[str, tuple]] = []

    def _record(self, name, *args):
        self.calls.append((name, args))
        if name in self.fail:
            raise ToolClientError(f"{name} is unavailable.")

    async def get_crop_resource_requirements(self, crop_plan_request_id, workflow_id, agent_step_id):
        self._record("GetCropResourceRequirements", crop_plan_request_id, workflow_id, agent_step_id)
        return CropResourceRequirements.model_validate(self.requirements)

    async def get_field_details(self, field_id, workflow_id, agent_step_id):
        self._record("GetFieldDetails", field_id, workflow_id, agent_step_id)
        return {"id": str(FIELD_ID), "area": self.requirements.get("fieldArea"), "soilType": "Loam"}

    async def get_resource_availability(self, resource_ids, workflow_id, agent_step_id):
        self._record("GetResourceAvailability", tuple(resource_ids), workflow_id, agent_step_id)
        return [StockSnapshot.model_validate(item) for item in self.stocks]

    async def get_existing_reservations(self, resource_ids, workflow_id, agent_step_id):
        self._record("GetExistingReservations", tuple(resource_ids), workflow_id, agent_step_id)
        return [ReservationSnapshot.model_validate(item) for item in self.reservations]

    async def get_low_stock_status(self, workflow_id, agent_step_id):
        self._record("GetLowStockStatus", workflow_id, agent_step_id)
        return [StockSnapshot.model_validate(item) for item in self.stocks if item["availableQuantity"] <= item["lowStockThreshold"]]

    async def get_weather_forecast(self, workflow_id, agent_step_id):
        self._record("GetWeatherForecast", workflow_id, agent_step_id)
        return WeatherForecast.model_validate(self.weather)


async def run(tools: FakeTools, **kwargs):
    return await WeatherResourceAgent(tools=tools).run(request_input(**kwargs))


# TEST 1: verified requirement + sufficient inventory.
@pytest.mark.asyncio
async def test_requirement_with_sufficient_inventory_is_sufficient():
    tools = FakeTools(stocks=[stock(available=60)])

    result = await run(tools)

    item = result.resource_requirements[0]
    assert (item.required_quantity, item.available_quantity, item.shortage_quantity) == (50, 60, 0)
    assert (item.sufficient, item.requirement_status) == (True, "Sufficient")
    assert item.basis == "100 kg/acre x 0.5 acre = 50 kg"
    assert result.requirement_status == "Sufficient"
    assert result.requires_human_review is False
    assert result.resource_checks[0].requested == 50 and result.resource_checks[0].requirement_status == "Sufficient"
    assert result.requirement_source is not None and result.requirement_source.source_name == "SAMPLE source (test)"


# TEST 2: verified requirement + insufficient inventory (the worked example: 50 kg needed, 30 kg available).
@pytest.mark.asyncio
async def test_requirement_with_insufficient_inventory_reports_shortage():
    result = await run(FakeTools(stocks=[stock(available=30)]))

    item = result.resource_requirements[0]
    assert (item.required_quantity, item.available_quantity, item.shortage_quantity) == (50, 30, 20)
    assert (item.sufficient, item.requirement_status) == (False, "Insufficient")
    assert result.requirement_status == "Insufficient"
    assert result.requires_human_review is True
    assert result.reason.startswith("Required resource quantity exceeds currently available inventory")
    assert any("shortage 20 kg" in warning for warning in result.warnings)
    assert any("Obtain at least 20 kg more Urea" in text for text in result.recommendations)


# TEST 3: no verified crop-resource requirement.
@pytest.mark.asyncio
async def test_missing_verified_requirement_is_unknown_and_never_guessed():
    result = await run(FakeTools(requirements=requirements_result(verified=False)))

    assert result.requirement_status == "ResourceRequirementUnknown"
    summary = result.resource_requirements[0]
    assert summary.rule_id is None and summary.required_quantity is None and summary.sufficient is None
    assert summary.reason == "No verified crop-resource requirement is available for Tomato."
    assert result.resource_checks[0].requested is None
    assert result.resource_checks[0].requirement_status == "ResourceRequirementUnknown"
    assert result.requires_human_review is True
    assert result.status == "Analyzed"


# TEST 4: reservations reduce available stock; the agent uses the net available quantity.
@pytest.mark.asyncio
async def test_reservations_reduce_available_quantity_used_for_the_comparison():
    reservation = {
        "reservationId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "inventoryStockId": str(STOCK_ID), "resourceId": str(UREA_ID),
        "resourceName": "Urea", "unit": "kg", "quantity": 40, "purpose": "Other plan", "createdAt": "2026-09-01T00:00:00Z",
    }
    # 70 kg on hand, 40 kg reserved: 30 kg available, so 50 kg is not covered even though on-hand stock would be.
    result = await run(FakeTools(stocks=[stock(available=30, reserved=40)], reservations=[reservation]))

    item = result.resource_requirements[0]
    assert (item.available_quantity, item.reserved_quantity, item.shortage_quantity) == (30, 40, 20)
    assert item.requirement_status == "Insufficient"
    assert "1 active reservation(s) totalling 40 kg" in item.reason


# TEST 5: weather risk with sufficient resources - reported separately.
@pytest.mark.asyncio
async def test_weather_risk_is_reported_separately_from_sufficient_resources():
    result = await run(FakeTools(stocks=[stock(available=60)], weather=forecast(rain=35)))

    assert result.weather_risk == "High"
    assert result.requirement_status == "Sufficient"
    assert result.resource_requirements[0].requirement_status == "Sufficient"
    assert result.requires_human_review is True
    assert "Weather risk is High" in result.reason
    assert "agricultural officer" in result.recommendations[0]


# TEST 6: weather risk and insufficient resources - both reflected.
@pytest.mark.asyncio
async def test_weather_risk_and_insufficient_resources_are_both_reflected():
    result = await run(FakeTools(stocks=[stock(available=30)], weather=forecast(temperature=39)))

    assert result.weather_risk == "High"
    assert result.requirement_status == "Insufficient"
    assert "exceeds currently available inventory" in result.reason and "Weather risk is High" in result.reason
    assert any("agricultural officer" in text for text in result.recommendations)
    assert any("Obtain at least 20 kg more Urea" in text for text in result.recommendations)


# TEST 7: no field area - no fake requirement.
@pytest.mark.asyncio
async def test_missing_field_area_does_not_produce_a_requirement():
    result = await run(FakeTools(requirements=requirements_result(field_area=None)))

    item = result.resource_requirements[0]
    assert item.required_quantity is None and item.shortage_quantity is None and item.sufficient is None
    assert item.requirement_status == "ResourceRequirementUnknown"
    assert item.reason == "The field area is not recorded."
    assert result.requirement_status == "ResourceRequirementUnknown"
    assert result.resource_checks[0].requested is None


@pytest.mark.asyncio
async def test_agent_calls_only_read_tools_with_workflow_scope():
    tools = FakeTools()

    result = await run(tools)

    names = [name for name, _ in tools.calls]
    assert names == [
        "GetCropResourceRequirements", "GetFieldDetails", "GetResourceAvailability",
        "GetExistingReservations", "GetLowStockStatus", "GetWeatherForecast",
    ]
    assert all(name.startswith("Get") for name in names)
    assert all(WORKFLOW_ID in args and STEP_ID in args for _, args in tools.calls)
    assert dict(tools.calls)["GetResourceAvailability"][0] == (UREA_ID,)
    assert result.tools_used == names


@pytest.mark.asyncio
async def test_resource_not_in_inventory_catalogue_is_insufficient_with_zero_available():
    result = await run(FakeTools(requirements=requirements_result(match="NotInCatalogue"), stocks=[]))

    item = result.resource_requirements[0]
    assert (item.available_quantity, item.shortage_quantity, item.requirement_status) == (0, 50, "Insufficient")
    assert result.requirement_status == "Insufficient"


@pytest.mark.asyncio
async def test_unit_mismatch_is_not_compared():
    result = await run(FakeTools(requirements=requirements_result(unit="g")))

    item = result.resource_requirements[0]
    assert item.requirement_status == "InventoryNotComparable"
    assert item.shortage_quantity is None and item.sufficient is None
    assert result.requirement_status == "Incomplete"
    assert result.resource_checks[0].requirement_status == "InventoryNotComparable"


@pytest.mark.asyncio
async def test_requirement_tool_failure_is_unknown_not_invented():
    result = await run(FakeTools(fail={"GetCropResourceRequirements"}))

    assert result.status == "Analyzed"
    assert result.requirement_status == "ResourceRequirementUnknown"
    assert all(item.required_quantity is None for item in result.resource_requirements)
    assert any("GetCropResourceRequirements failed" in warning for warning in result.warnings)


@pytest.mark.asyncio
async def test_inventory_tool_failure_is_a_safe_failure():
    result = await run(FakeTools(fail={"GetResourceAvailability"}))

    assert result.status == "SafeFailure"
    assert result.requires_human_review is True
    assert result.resource_checks == [] and result.resource_requirements == []


@pytest.mark.asyncio
async def test_without_tools_the_agent_returns_a_safe_failure():
    result = await WeatherResourceAgent().run(request_input())

    assert result.status == "SafeFailure"
    assert result.requires_human_review is True


@pytest.mark.asyncio
async def test_safe_member2_context_is_read_only_and_does_not_change_resource_reasoning():
    request = request_input().model_copy(update={"member_2_field_analysis_context": None})
    with_context = WeatherResourceInput.model_validate({
        **request.model_dump(by_alias=True, mode="json"),
        "member2FieldAnalysisContext": {
            "fieldSuitability": "SuitableWithConditions", "soilAssessment": "Soil Loamy.", "waterAssessment": "Water Adequate.",
            "drainageAssessment": "Drainage Poor.", "fieldPreparationRequirements": ["Clear drainage channels."],
            "plantingReadiness": "RequiresPreparation", "identifiedRisks": ["PoorDrainage"],
            "recommendedPrePlantingActions": ["Address drainage."], "priority": "Low", "warnings": [], "requiresHumanReview": True,
        },
    })

    plain = await WeatherResourceAgent(tools=FakeTools()).run(request)
    contextual = await WeatherResourceAgent(tools=FakeTools()).run(with_context)

    assert with_context.member_2_field_analysis_context.planting_readiness == "RequiresPreparation"
    assert contextual.resource_requirements == plain.resource_requirements
    assert contextual.weather_risk == plain.weather_risk


# Existing weather and low-stock behaviour, unchanged.
@pytest.mark.asyncio
async def test_low_risk_preserves_inventory_snapshot_ids_and_values():
    result = await run(FakeTools(stocks=[stock(available=20, threshold=8)]))

    assert result.status == "Analyzed"
    assert result.weather_risk == "Low"
    assert result.resource_checks[0].inventory_stock_id == STOCK_ID
    assert result.resource_checks[0].available_quantity == 20
    assert result.resource_checks[0].is_low_stock is False


@pytest.mark.asyncio
async def test_medium_weather_threshold_is_deterministic():
    result = await run(FakeTools(weather=forecast(temperature=34)))

    assert result.weather_risk == "Medium"
    assert "maximum temperature 34 C" in result.weather_summary


@pytest.mark.asyncio
async def test_missing_forecast_returns_unknown_without_inventing_weather():
    result = await run(FakeTools(weather=forecast(available=False)))

    assert result.weather_risk == "Unknown"
    assert result.requires_human_review is True
    assert "could not be calculated" in result.warnings[0]


@pytest.mark.asyncio
async def test_weather_tool_failure_returns_unknown_weather():
    result = await run(FakeTools(fail={"GetWeatherForecast"}))

    assert result.weather_risk == "Unknown"
    assert "GetWeatherForecast" not in result.tools_used


@pytest.mark.asyncio
async def test_low_stock_is_flagged_without_reserving_inventory():
    result = await run(FakeTools(stocks=[stock(available=6, threshold=8)]))

    assert result.resource_checks[0].is_low_stock is True
    assert result.resource_checks[0].available_quantity == 6
    assert "Restock Urea: only 6 kg available." in result.recommendations


@pytest.mark.asyncio
async def test_missing_inventory_snapshot_requires_review():
    result = await run(FakeTools(requirements=requirements_result(verified=False), stocks=[]))

    assert result.requires_human_review is True
    assert result.resource_checks == []
    assert any("No active inventory rows" in warning for warning in result.warnings)


def test_weather_resource_endpoint_runs_without_an_llm_provider(monkeypatch):
    from fastapi.testclient import TestClient

    import main
    from config import Settings, get_settings

    # Without OPENAI_API_KEY no provider exists: every figure is still calculated and the explanation is rule-based.
    settings = Settings(_env_file=None, AI_SERVICE_TOKEN="test-service-token", AI_PROVIDER="openai", OPENAI_API_KEY="")
    main.app.dependency_overrides[get_settings] = lambda: settings
    tools = FakeTools()
    monkeypatch.setattr(main, "WeatherResourceTools", lambda client: tools)
    try:
        response = TestClient(main.app).post(
            "/workflows/crop-planning/weather-resource",
            json=request_input().model_dump(mode="json", by_alias=True),
            headers={"X-AgriAssist-AI-Token": "test-service-token"},
        )
    finally:
        main.app.dependency_overrides.clear()

    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "Analyzed"
    assert body["requirementStatus"] == "Insufficient"  # sample fixture: 50 kg required, 30 kg available
    assert "GetResourceAvailability" in body["toolsUsed"]
    assert body["weatherRiskAssessment"]["generatedBy"] == "RuleBased"
    assert body["weatherRiskAssessment"]["riskLevel"] == body["weatherRisk"]


# Weather risk explanation: calculated factors, OpenAI narrative only when it stays grounded in them.
class FakeProvider:
    provider_name = "fake"

    def __init__(self, payload: dict | None = None, error: Exception | None = None) -> None:
        self.payload = payload
        self.error = error
        self.requests = []

    async def generate_structured_json(self, request):
        self.requests.append(request)
        if self.error is not None:
            raise self.error
        return LLMResponse(text=json.dumps(self.payload))


# SAMPLE forecast (one day, 2026-09-15): 12 mm rain, 31 C, 5 m/s, so only daily rain reaches Medium.
GROUNDED_NARRATIVE = {
    "headline": "Medium weather risk: 12 mm of rain is expected on 2026-09-15.",
    "explanation": "The heaviest daily rain of 12 mm on 2026-09-15 reaches the 10 mm Medium threshold. "
                   "The temperature of 31 C and wind of 5 m/s stay below their thresholds.",
    "potentialImpacts": ["Rain can delay land preparation for the Tomato plan and wash fertilizer away."],
    "recommendedActions": [
        {"action": "Clear drainage channels before the rain.", "timing": "Before 2026-09-15", "priority": "Medium"},
    ],
    "monitoringAdvice": "Check the forecast again the day before field work.",
}


async def run_with_provider(provider, tools=None):
    agent = WeatherResourceAgent(tools=tools or FakeTools(weather=forecast(rain=12)), llm_provider=provider)
    return await agent.run(request_input())


@pytest.mark.asyncio
async def test_rule_based_assessment_explains_why_the_risk_is_medium():
    result = await run(FakeTools(weather=forecast(rain=12)))

    assessment = result.weather_risk_assessment
    assert result.weather_risk == "Medium" and assessment.risk_level == "Medium"
    assert assessment.generated_by == "RuleBased"
    factors = {factor.metric: factor for factor in assessment.contributing_factors}
    assert set(factors) == {"DailyRainfall", "TotalRainfall", "MaxTemperature", "MaxWind"}
    assert (factors["DailyRainfall"].value, factors["DailyRainfall"].level) == (12, "Medium")
    assert factors["DailyRainfall"].observed_on.isoformat() == "2026-09-15"
    assert (factors["MaxTemperature"].value, factors["MaxTemperature"].level) == (31, "Low")
    assert "Medium" in assessment.headline
    assert "12 mm on 2026-09-15" in assessment.explanation and "Medium threshold of 10 mm" in assessment.explanation
    assert any("waterlog" in impact for impact in assessment.potential_impacts)
    assert any(action.timing == "Before 2026-09-15" for action in assessment.recommended_actions)
    # The shortage from the sample fixture (50 kg needed, 30 kg available) is connected to the weather impact.
    assert any("Urea" in impact for impact in assessment.potential_impacts)


@pytest.mark.asyncio
async def test_low_risk_assessment_says_every_measure_is_below_its_threshold():
    result = await run(FakeTools())

    assessment = result.weather_risk_assessment
    assert assessment.risk_level == "Low"
    assert all(factor.level == "Low" for factor in assessment.contributing_factors)
    assert "below its Medium threshold" in assessment.explanation
    assert [action.priority for action in assessment.recommended_actions] == ["Low", "Low"]


@pytest.mark.asyncio
async def test_drainage_concern_from_field_analysis_is_reflected_in_rain_impacts():
    request = WeatherResourceInput.model_validate({
        **request_input().model_dump(by_alias=True, mode="json"),
        "member2FieldAnalysisContext": {
            "fieldSuitability": "SuitableWithConditions", "soilAssessment": "Soil Loamy.", "waterAssessment": "Water Adequate.",
            "drainageAssessment": "Drainage Poor.", "fieldPreparationRequirements": [], "plantingReadiness": "RequiresPreparation",
            "identifiedRisks": ["PoorDrainage"], "recommendedPrePlantingActions": [], "priority": "Low", "warnings": [],
            "requiresHumanReview": True,
        },
    })

    result = await WeatherResourceAgent(tools=FakeTools(weather=forecast(rain=35))).run(request)

    assert result.weather_risk_assessment.risk_level == "High"
    assert any("drainage or flooding concern" in impact for impact in result.weather_risk_assessment.potential_impacts)
    assert any("agricultural officer" in action.action for action in result.weather_risk_assessment.recommended_actions)


@pytest.mark.asyncio
async def test_grounded_openai_narrative_is_used_with_calculated_factors():
    provider = FakeProvider(GROUNDED_NARRATIVE)

    result = await run_with_provider(provider)

    assessment = result.weather_risk_assessment
    assert assessment.generated_by == "OpenAI"
    assert assessment.headline == GROUNDED_NARRATIVE["headline"]
    assert assessment.recommended_actions[0].action == "Clear drainage channels before the rain."
    assert {factor.metric: factor.level for factor in assessment.contributing_factors}["DailyRainfall"] == "Medium"
    # Only the narrative is generated: risk, recommendations and resource figures are unchanged.
    assert result.weather_risk == "Medium"
    assert result.resource_requirements[0].shortage_quantity == 20
    request = provider.requests[0]
    assert request.schema_name == "weather_risk_explanation_v1"
    facts = json.loads(request.user_input.removeprefix("FACTS:\n"))
    assert facts["riskLevel"] == "Medium"
    assert facts["forecast"]["days"][0]["rainMm"] == 12
    assert facts["cropPlan"]["cropName"] == "Tomato"
    assert facts["resourceStatus"]["items"][0]["shortageQuantity"] == 20
    assert str(WORKFLOW_ID) not in request.user_input and str(UREA_ID) not in request.user_input


@pytest.mark.asyncio
@pytest.mark.parametrize("change", [
    {"explanation": "Expect 55 mm of rain, so apply 75 kg of Urea per acre after it."},
    {"headline": "High weather risk because of rain on 2026-09-15."},
    {"potentialImpacts": ["Humid weather may need a fungicide spray."]},
])
async def test_unsupported_openai_narrative_falls_back_to_rules(change):
    result = await run_with_provider(FakeProvider({**GROUNDED_NARRATIVE, **change}))

    assert result.weather_risk_assessment.generated_by == "RuleBased"
    assert result.weather_risk_assessment.risk_level == "Medium"


@pytest.mark.asyncio
@pytest.mark.parametrize("provider", [
    FakeProvider(error=LLMProviderError("OpenAI structured generation timed out.", category="timeout")),
    FakeProvider({"headline": "Missing fields"}),
])
async def test_provider_failure_keeps_the_analysis_with_a_rule_based_explanation(provider):
    result = await run_with_provider(provider)

    assert result.status == "Analyzed"
    assert result.weather_risk == "Medium"
    assert result.weather_risk_assessment.generated_by == "RuleBased"


@pytest.mark.asyncio
async def test_unknown_weather_is_explained_without_calling_the_provider():
    provider = FakeProvider(GROUNDED_NARRATIVE)

    result = await run_with_provider(provider, FakeTools(weather=forecast(available=False)))

    assessment = result.weather_risk_assessment
    assert provider.requests == []
    assert (assessment.risk_level, assessment.generated_by, assessment.contributing_factors) == ("Unknown", "RuleBased", [])
    assert "no forecast was available" in assessment.explanation


@pytest.mark.asyncio
async def test_safe_failure_has_no_weather_risk_assessment():
    result = await run(FakeTools(fail={"GetResourceAvailability"}))

    assert result.status == "SafeFailure"
    assert result.weather_risk_assessment is None
