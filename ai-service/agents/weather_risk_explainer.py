"""Member 3 weather-risk explanation used by WeatherResourceAgent.

The risk level and its contributing factors are calculated here from the forecast with fixed thresholds. OpenAI only
writes the narrative around them (why the level, likely impact, what the farmer should do). The narrative is checked
against the facts it was given and is replaced by a rule-based explanation when no provider is configured, the
provider fails, or the text claims another risk level, cites unsupported figures or suggests chemical treatment.
"""

import json
import logging
import re
from datetime import date
from typing import Annotated, Any

from pydantic import ConfigDict, Field, StringConstraints

from providers.base_llm_provider import BaseLLMProvider, StructuredGenerationRequest
from schemas.common import CamelModel, to_camel
from schemas.weather_resource import (
    ADVICE_TEXT,
    INSUFFICIENT,
    CropResourceRequirements,
    Member2FieldAnalysisContext,
    ResourceRequirementAssessment,
    WeatherForecast,
    WeatherResourceInput,
    WeatherRiskAction,
    WeatherRiskAssessment,
    WeatherRiskFactor,
)

logger = logging.getLogger(__name__)

WEATHER_RISK_EXPLANATION_CONTRACT_VERSION = 1

# metric: (label, unit, Medium threshold, High threshold). The ASP.NET output validator mirrors these values.
RISK_THRESHOLDS: dict[str, tuple[str, str, float, float]] = {
    "DailyRainfall": ("Heaviest daily rain", "mm", 10, 30),
    "TotalRainfall": ("Total forecast rain", "mm", 30, 80),
    "MaxTemperature": ("Highest temperature", "C", 34, 38),
    "MaxWind": ("Strongest wind", "m/s", 10, 15),
}
_LEVEL_ORDER = {"Low": 0, "Medium": 1, "High": 2}
_DRIVER_PHRASES = {
    "DailyRainfall": "heavy rain on a single day",
    "TotalRainfall": "high total rainfall",
    "MaxTemperature": "high temperatures",
    "MaxWind": "strong wind",
}
_NUMBER = re.compile(r"\d+(?:\.\d+)?")
_CHEMICAL_ADVICE = re.compile(
    r"\b(pesticides?|fungicides?|herbicides?|insecticides?|chemical treatments?|dosages?|doses?)\b", re.IGNORECASE
)

SYSTEM_PROMPT = (
    "You are the weather advisor of AgriAssist, a pre-cultivation farm planning system used in Sri Lanka. "
    "The weather risk level in FACTS was already decided by fixed rules from the forecast. Explain it; never change it.\n"
    "Write for the Resource Officer and the farmer:\n"
    "- headline: one sentence that starts with '<riskLevel> weather risk' and names the main driver.\n"
    "- explanation: 2-4 sentences on why the risk is at this level. Cite the driving factors with their values, dates "
    "and the threshold each reached, and say which measures stayed below their thresholds.\n"
    "- potentialImpacts: 2-4 specific impacts on this crop, field and plan (for example waterlogging, delayed land "
    "preparation or sowing, fertilizer washed away, heat or wind stress on young plants). Use the field context and "
    "resource status when they make an impact more or less likely.\n"
    "- recommendedActions: 3-5 practical actions the farmer should take, each with a timing tied to the forecast dates "
    "or the plan stage and a priority of High, Medium or Low.\n"
    "- monitoringAdvice: 1-2 sentences on what to watch and when to check the forecast again.\n"
    "Rules: use only FACTS. Every number you write must appear in FACTS; describe any other timing in words "
    "(for example 'the day before the heaviest rain', 'early morning'). Do not invent rainfall, temperature, wind, "
    "quantities, rates, prices or dates. Do not recommend pesticides, fungicides, herbicides, insecticides, chemical "
    "treatments or doses. Do not approve, schedule or reserve anything: final tasks need human approval. "
    "If the risk is Low, say so plainly and keep the actions light. Use plain, concise English."
)

NARRATIVE_SCHEMA: dict[str, Any] = {
    "type": "object",
    "additionalProperties": False,
    "required": ["headline", "explanation", "potentialImpacts", "recommendedActions", "monitoringAdvice"],
    "properties": {
        "headline": {"type": "string"},
        "explanation": {"type": "string"},
        "potentialImpacts": {"type": "array", "items": {"type": "string"}},
        "recommendedActions": {
            "type": "array",
            "items": {
                "type": "object",
                "additionalProperties": False,
                "required": ["action", "timing", "priority"],
                "properties": {
                    "action": {"type": "string"},
                    "timing": {"type": "string"},
                    "priority": {"type": "string", "enum": ["High", "Medium", "Low"]},
                },
            },
        },
        "monitoringAdvice": {"type": "string"},
    },
}


class _Narrative(CamelModel):
    """The only part OpenAI writes. Lists longer than the output limits are trimmed, not rejected."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="forbid")

    headline: Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=200)]
    explanation: Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=1600)]
    potential_impacts: list[ADVICE_TEXT] = Field(alias="potentialImpacts", min_length=1)
    recommended_actions: list[WeatherRiskAction] = Field(alias="recommendedActions", min_length=1)
    monitoring_advice: Annotated[str, StringConstraints(strip_whitespace=True, max_length=800)] = Field(alias="monitoringAdvice")


def risk_factors(weather: WeatherForecast) -> list[WeatherRiskFactor]:
    """The four measures behind the weather risk, compared with the fixed thresholds. Empty without a forecast."""
    if not weather.is_available or not weather.days:
        return []
    days = sorted(weather.days, key=lambda day: day.date)
    wettest = max(days, key=lambda day: day.rain_mm)
    hottest = max(days, key=lambda day: day.max_temperature_c)
    windiest = max(days, key=lambda day: day.max_wind_speed_ms)
    return [
        _factor("DailyRainfall", wettest.rain_mm, wettest.date),
        _factor("TotalRainfall", sum(day.rain_mm for day in days), None, len(days)),
        _factor("MaxTemperature", hottest.max_temperature_c, hottest.date),
        _factor("MaxWind", windiest.max_wind_speed_ms, windiest.date),
    ]


def overall_risk(factors: list[WeatherRiskFactor]) -> str:
    """High when any measure reaches its High threshold, Medium when any reaches Medium, otherwise Low."""
    if not factors:
        return "Unknown"
    return max((factor.level for factor in factors), key=_LEVEL_ORDER.__getitem__)


class WeatherRiskExplainer:
    def __init__(self, provider: BaseLLMProvider | None = None, timeout_seconds: float = 30) -> None:
        self._provider = provider
        self._timeout_seconds = timeout_seconds

    async def explain(
        self,
        *,
        risk: str,
        factors: list[WeatherRiskFactor],
        weather: WeatherForecast,
        request: WeatherResourceInput,
        requirements: CropResourceRequirements | None,
        assessments: list[ResourceRequirementAssessment],
        requirement_status: str,
    ) -> WeatherRiskAssessment:
        days = sorted(weather.days, key=lambda day: day.date) if weather.is_available else []
        fallback = rule_based_assessment(
            risk,
            factors,
            location=request.location or weather.location,
            forecast_start=days[0].date if days else None,
            forecast_end=days[-1].date if days else None,
            preferred_start=request.preferred_start_date,
            field_context=request.member_2_field_analysis_context,
            short_resources=[item.resource_name for item in assessments if item.requirement_status == INSUFFICIENT],
            unavailable_message=weather.message,
        )
        # Nothing to explain without a forecast; the rule-based text already says so without guessing.
        if self._provider is None or risk == "Unknown" or not factors:
            return fallback

        facts = _facts(risk, factors, days, request, requirements, assessments, requirement_status)
        facts_json = json.dumps(facts, ensure_ascii=False, separators=(",", ":"))
        try:
            generated = await self._provider.generate_structured_json(
                StructuredGenerationRequest(
                    schema_name="weather_risk_explanation_v1",
                    contract_version=WEATHER_RISK_EXPLANATION_CONTRACT_VERSION,
                    response_schema=NARRATIVE_SCHEMA,
                    system_input=SYSTEM_PROMPT,
                    user_input=f"FACTS:\n{facts_json}",
                    timeout_seconds=self._timeout_seconds,
                )
            )
            narrative = _Narrative.model_validate_json(generated.text)
        except Exception as exc:  # The narrative is optional: any provider or parsing problem uses the rules.
            logger.warning("Weather risk explanation used the rules: %s", getattr(exc, "category", type(exc).__name__))
            return fallback

        problem = _unsupported_content(narrative, risk, facts_json)
        if problem:
            logger.warning("Weather risk explanation used the rules: %s", problem)
            return fallback

        headline = narrative.headline
        if risk.casefold() not in headline.casefold():
            headline = f"{risk} weather risk: {headline}"
        return WeatherRiskAssessment(
            riskLevel=risk,
            headline=headline,
            explanation=narrative.explanation,
            contributingFactors=factors,
            potentialImpacts=narrative.potential_impacts[:6],
            recommendedActions=narrative.recommended_actions[:6],
            monitoringAdvice=narrative.monitoring_advice,
            generatedBy="OpenAI",
        )


def rule_based_assessment(
    risk: str,
    factors: list[WeatherRiskFactor],
    *,
    location: str,
    forecast_start: date | None,
    forecast_end: date | None,
    preferred_start: date,
    field_context: Member2FieldAnalysisContext | None,
    short_resources: list[str],
    unavailable_message: str = "",
) -> WeatherRiskAssessment:
    """Deterministic explanation used without OpenAI. It only restates calculated factors and fixed guidance."""
    place = location or "the farm"
    if risk == "Unknown" or not factors or forecast_start is None or forecast_end is None:
        reason = (unavailable_message.strip() or "No forecast was returned by the weather provider.")[:300].rstrip(".")
        return WeatherRiskAssessment(
            riskLevel="Unknown",
            headline=f"Weather risk is Unknown for {place}: no forecast was available."[:240],
            explanation=f"The weather risk could not be calculated because no forecast was available ({reason}). "
                        "No rainfall, temperature or wind figures were assumed.",
            potentialImpacts=[
                "Weather-sensitive work such as land preparation, sowing and fertilizer application cannot be timed "
                "safely without a forecast."
            ],
            recommendedActions=[
                _action("Obtain a current forecast before confirming weather-sensitive tasks.", "Before scheduling", "High"),
                _action("Ask an agricultural officer to review the timing of field work until a forecast is available.",
                        "Before scheduling", "Medium"),
            ],
            monitoringAdvice="Run the analysis again once the weather provider returns a forecast.",
            generatedBy="RuleBased",
        )

    drivers =[factor for factor in factors if factor.level == risk] if risk != "Low" else []
    elevated = [factor for factor in factors if factor.level != "Low" and factor not in drivers]
    calm = [factor for factor in factors if factor.level == "Low"]

    if drivers:
        headline = f"{risk} weather risk for {place}, driven by {_join(_DRIVER_PHRASES[f.metric] for f in drivers)}."
        parts = [f"The risk is {risk} because of {_join(_DRIVER_PHRASES[f.metric] for f in drivers)}."]
        parts += [factor.detail for factor in drivers]
        if elevated:
            parts.append("Also elevated to Medium: " + _join(_figure(f) for f in elevated) + ".")
        if calm:
            parts.append("Below their Medium thresholds: " + _join(_figure(f) for f in calm) + ".")
    else:
        headline = f"Low weather risk for {place}: every forecast measure is below its Medium threshold."
        parts = ["The risk is Low because every measure stays below its Medium threshold: "
                 + _join(f"{_figure(f)} (Medium from {_fmt(f.medium_threshold)} {f.unit})" for f in factors) + "."]
    parts.append(f"The forecast covers {forecast_start.isoformat()} to {forecast_end.isoformat()}.")

    raised = {factor.metric: factor for factor in factors if factor.level != "Low"}
    rain = [raised[metric] for metric in ("DailyRainfall", "TotalRainfall") if metric in raised]
    heat = raised.get("MaxTemperature")
    wind = raised.get("MaxWind")

    impacts: list[str] = []
    if rain:
        impacts.append("Heavy or prolonged rain can waterlog the field, delay land preparation and sowing, and wash "
                       "freshly applied fertilizer away.")
        if _has_drainage_concern(field_context):
            impacts.append("The field analysis already reports a drainage or flooding concern, so standing water is "
                           "more likely on this field.")
    if heat:
        impacts.append("High temperatures can stress seedlings and transplants and increase irrigation demand.")
        if _has_water_concern(field_context):
            impacts.append("The field analysis reports limited water, so heat stress is harder to offset with irrigation.")
    if wind:
        impacts.append("Strong wind can damage young plants and supports and makes fertilizer broadcasting uneven.")
    if drivers and short_resources:
        impacts.append(f"Resource shortages ({_join(short_resources[:3])}) could push field work into the higher-risk days.")
    if not impacts:
        impacts.append("No significant weather impact is expected during the forecast period.")

    actions: list[WeatherRiskAction] = []
    if rain:
        peak = raised.get("DailyRainfall")
        timing = f"Before {peak.observed_on.isoformat()}" if peak and peak.observed_on else "During the forecast period"
        priority = "High" if any(f.level == "High" for f in rain) else "Medium"
        actions.append(_action("Avoid sowing or applying fertilizer just before or during the heaviest rain; use the "
                               "drier forecast days instead.", timing, priority))
        actions.append(_action("Clear drainage channels and field outlets so excess water can drain away.", timing, priority))
    if risk == "High":
        actions.append(_action("Ask an agricultural officer to review the weather hazard before scheduling field work.",
                               "Before scheduling", "High"))
    if heat:
        timing = f"Around {heat.observed_on.isoformat()}" if heat.observed_on else "During the hottest days"
        actions.append(_action("Irrigate in the early morning or evening and keep the soil moist through the hottest days.",
                               timing, heat.level))
        actions.append(_action("Avoid transplanting seedlings on the hottest days.", timing, "Medium"))
    if wind:
        timing = f"On {wind.observed_on.isoformat()}" if wind.observed_on else "On windy days"
        actions.append(_action("Postpone fertilizer broadcasting and other wind-sensitive work; secure young plants and supports.",
                               timing, wind.level))
    if not drivers:
        actions.append(_action("Continue normal field preparation; no weather-specific precautions are needed.",
                               "During the forecast period", "Low"))
    actions.append(_action("Check the forecast again before confirming weather-sensitive field work.", "Before scheduling",
                           "Medium" if drivers else "Low"))

    monitoring = f"The forecast covers {forecast_start.isoformat()} to {forecast_end.isoformat()}."
    if preferred_start > forecast_end:
        monitoring += (f" The preferred planting window starts on {preferred_start.isoformat()}, after this forecast "
                       "ends, so check the forecast again closer to planting.")
    elif forecast_start <= preferred_start:
        monitoring += (f" The preferred planting window starts on {preferred_start.isoformat()}, inside this forecast, "
                       "so watch the days around it closely.")
    monitoring += " Re-check before confirming weather-sensitive tasks."

    return WeatherRiskAssessment(
        riskLevel=risk,
        headline=headline[:240],
        explanation=" ".join(parts)[:1600],
        contributingFactors=factors,
        potentialImpacts=impacts[:6],
        recommendedActions=actions[:6],
        monitoringAdvice=monitoring,
        generatedBy="RuleBased",
    )


def _factor(metric: str, raw_value: float, observed_on: date | None, day_count: int = 0) -> WeatherRiskFactor:
    label, unit, medium, high = RISK_THRESHOLDS[metric]
    value = round(raw_value, 3)
    level = "High" if value >= high else "Medium" if value >= medium else "Low"
    where = f"over {day_count} forecast day(s)" if observed_on is None else f"on {observed_on.isoformat()}"
    if level == "High":
        comparison = f"at or above the High threshold of {_fmt(high)} {unit}"
    elif level == "Medium":
        comparison = f"at or above the Medium threshold of {_fmt(medium)} {unit} (High from {_fmt(high)} {unit})"
    else:
        comparison = f"below the Medium threshold of {_fmt(medium)} {unit}"
    return WeatherRiskFactor(
        metric=metric,
        label=label,
        value=value,
        unit=unit,
        observedOn=observed_on,
        mediumThreshold=medium,
        highThreshold=high,
        level=level,
        detail=f"{label} is {_fmt(value)} {unit} {where}, {comparison}.",
    )


def _facts(
    risk: str,
    factors: list[WeatherRiskFactor],
    days: list,
    request: WeatherResourceInput,
    requirements: CropResourceRequirements | None,
    assessments: list[ResourceRequirementAssessment],
    requirement_status: str,
) -> dict[str, Any]:
    """Everything the LLM may use. No IDs are included, so every number in it is a real figure."""
    context = request.member_2_field_analysis_context
    field_context: dict[str, Any] = {"summary": request.field_analysis_summary[:600]}
    if context is not None:
        field_context |= {
            "soil": context.soil_assessment,
            "water": context.water_assessment,
            "drainage": context.drainage_assessment,
            "plantingReadiness": context.planting_readiness,
            "identifiedRisks": context.identified_risks[:8],
            "preparationRequirements": context.field_preparation_requirements[:5],
        }
    return {
        "riskLevel": risk,
        "riskRule": "High when any measure reaches its High threshold, Medium when any reaches its Medium threshold, "
                    "otherwise Low.",
        "location": request.location,
        "forecast": {
            "start": days[0].date.isoformat(),
            "end": days[-1].date.isoformat(),
            "dayCount": len(days),
            "days": [day.model_dump(mode="json", by_alias=True) for day in days],
        },
        "contributingFactors": [factor.model_dump(mode="json", by_alias=True) for factor in factors],
        "cropPlan": {
            "cropName": requirements.crop_name if requirements else None,
            "varietyName": requirements.variety_name if requirements else None,
            "fieldArea": requirements.field_area if requirements else None,
            "fieldAreaUnit": requirements.field_area_unit if requirements else None,
            "preferredStartDate": request.preferred_start_date.isoformat(),
            "preferredEndDate": request.preferred_end_date.isoformat(),
            "fieldPriority": request.field_priority,
        },
        "fieldContext": field_context,
        "resourceStatus": {
            "overall": requirement_status,
            "items": [
                {
                    "resourceName": item.resource_name,
                    "unit": item.unit,
                    "requiredQuantity": item.required_quantity,
                    "availableQuantity": item.available_quantity,
                    "shortageQuantity": item.shortage_quantity,
                    "status": item.requirement_status,
                }
                for item in assessments
                if item.rule_id is not None
            ][:10],
        },
    }


def _unsupported_content(narrative: _Narrative, risk: str, facts_json: str) -> str | None:
    texts = [narrative.headline, narrative.explanation, narrative.monitoring_advice, *narrative.potential_impacts]
    texts += [text for action in narrative.recommended_actions for text in (action.action, action.timing)]
    combined = "\n".join(texts)
    if _CHEMICAL_ADVICE.search(combined):
        return "chemical treatment advice"
    for level in _LEVEL_ORDER:
        if level != risk and re.search(
            rf"\b{level}\s+weather\s+risk\b|\b(?:weather|overall)\s+risk\s+(?:level\s+)?(?:is|remains)\s+{level}\b",
            combined,
            re.IGNORECASE,
        ):
            return f"claimed {level} weather risk"
    facts = [float(token) for token in _NUMBER.findall(facts_json)]
    invented = [token for token in _NUMBER.findall(combined) if not _is_supported(token, facts)]
    return f"unsupported figures {sorted(set(invented))[:5]}" if invented else None


def _is_supported(token: str, facts: list[float]) -> bool:
    """A figure is supported when it is a small count (0-10) or a fact rounded to the precision it was written with."""
    value = float(token)
    decimals = len(token.split(".")[1]) if "." in token else 0
    if decimals == 0 and value <= 10:
        return True
    tolerance = 0.5 * 10 ** -decimals + 1e-9
    return any(abs(value - fact) <= tolerance for fact in facts)


def _has_drainage_concern(context: Member2FieldAnalysisContext | None) -> bool:
    if context is None:
        return False
    return bool({"PoorDrainage", "FloodingRisk"} & set(context.identified_risks)) or "poor" in context.drainage_assessment.casefold()


def _has_water_concern(context: Member2FieldAnalysisContext | None) -> bool:
    if context is None:
        return False
    water = context.water_assessment.casefold()
    return "WaterShortageRisk" in context.identified_risks or any(word in water for word in ("limited", "unavailable", "unreliable"))


def _action(action: str, timing: str, priority: str) -> WeatherRiskAction:
    return WeatherRiskAction(action=action, timing=timing, priority=priority)


def _figure(factor: WeatherRiskFactor) -> str:
    return f"{factor.label.lower()} {_fmt(factor.value)} {factor.unit}"


def _join(items) -> str:
    values = list(items)
    if len(values) <= 1:
        return "".join(values)
    return ", ".join(values[:-1]) + " and " + values[-1]


def _fmt(value: float) -> str:
    return f"{value:g}"
