from collections.abc import Awaitable
from typing import Any, TypeVar
from uuid import UUID

from schemas.weather_resource import (
    INCOMPLETE,
    INSUFFICIENT,
    NOT_COMPARABLE,
    REQUIREMENT_UNKNOWN,
    SUFFICIENT,
    CalculatedResourceRequirement,
    CropResourceRequirements,
    ReservationSnapshot,
    ResourceCheck,
    ResourceRequirementAssessment,
    StockSnapshot,
    WeatherForecast,
    WeatherResourceInput,
    WeatherResourceOutput,
)
from tools.backend_tool_client import ToolClientError
from tools.weather_resource_tools import (
    GET_CROP_RESOURCE_REQUIREMENTS,
    GET_EXISTING_RESERVATIONS,
    GET_FIELD_DETAILS,
    GET_LOW_STOCK_STATUS,
    GET_RESOURCE_AVAILABILITY,
    GET_WEATHER_FORECAST,
    WeatherResourceTools,
)

T = TypeVar("T")


class WeatherResourceAgent:
    """Member 3 agent. Gathers verified requirements, field, inventory, reservations and weather through the
    read-only backend tools, then reasons over that tool output with fixed rules. Nothing is estimated, and
    nothing is reserved: every figure in the output comes from a tool result or simple arithmetic on one.

    The agent does not use the shared LLM provider (OpenAI), so it works the same with or without OPENAI_API_KEY.
    Keeping facts and recommendations deterministic prevents invented weather, stock or fertilizer data."""

    def __init__(self, tools: WeatherResourceTools | None = None) -> None:
        self._tools = tools

    async def run(self, request: WeatherResourceInput) -> WeatherResourceOutput:
        if self._tools is None:
            return self._safe_failure(request, [], "Weather and resource tools are not configured; no assessment was generated.")

        tools_used: list[str] = []
        warnings: list[str] = []
        ids = (request.workflow_id, request.agent_step_id)

        # 1-3. Identify the crop, its field area and the verified requirements (the backend calculates rate x area).
        requirements: CropResourceRequirements | None = await self._call(
            tools_used, warnings, GET_CROP_RESOURCE_REQUIREMENTS,
            self._tools.get_crop_resource_requirements(request.crop_plan_request_id, *ids),
        )
        field: dict[str, Any] | None = None
        if request.field_id:
            field = await self._call(tools_used, warnings, GET_FIELD_DETAILS, self._tools.get_field_details(request.field_id, *ids))
        rules = requirements.requirements if requirements else []
        required_ids = self._required_resource_ids(rules)

        # 4. Inventory is mandatory: without it no resource statement can be made.
        try:
            stocks = await self._tools.get_resource_availability(required_ids, *ids)
            tools_used.append(GET_RESOURCE_AVAILABILITY)
        except (ToolClientError, ValueError) as exc:
            return self._safe_failure(request, tools_used, f"Inventory could not be retrieved through {GET_RESOURCE_AVAILABILITY}: {exc}")

        # 5-7. Reservations, low-stock status and weather.
        reservations: list[ReservationSnapshot] = await self._call(
            tools_used, warnings, GET_EXISTING_RESERVATIONS, self._tools.get_existing_reservations(required_ids, *ids)
        ) or []
        low_stock_rows: list[StockSnapshot] | None = await self._call(
            tools_used, warnings, GET_LOW_STOCK_STATUS, self._tools.get_low_stock_status(*ids)
        )
        weather: WeatherForecast | None = await self._call(
            tools_used, warnings, GET_WEATHER_FORECAST, self._tools.get_weather_forecast(*ids)
        )
        if weather is None:
            weather = WeatherForecast(location=request.location, isAvailable=False, message="The weather forecast tool call failed.")

        # 8. Reason over the tool results.
        risk, weather_summary, weather_warnings = self._analyze_weather(weather, request.location)
        warnings[:0] = weather_warnings
        stocks_by_resource = {stock.resource_id: stock for stock in stocks}
        checked_ids = set(required_ids)
        reserved_by_resource = self._reservations_by_resource(reservations)
        assessments = [self._assess(rule, stocks_by_resource, checked_ids, reserved_by_resource) for rule in rules]
        requirement_status = self._overall_status(requirements, assessments)
        if requirement_status == REQUIREMENT_UNKNOWN and not rules:
            reason = (requirements.reason if requirements else None) or "No verified crop-resource requirement is available."
            assessments.append(ResourceRequirementAssessment(
                resourceName=requirements.crop_name if requirements else "Crop plan",
                requirementStatus=REQUIREMENT_UNKNOWN,
                reason=reason,
            ))
            warnings.append(f"{REQUIREMENT_UNKNOWN}: {reason} Resource sufficiency was not determined.")
        warnings.extend(self._requirement_warnings(assessments, stocks_by_resource))
        if requirements and field and field.get("area") is not None and requirements.field_area is not None \
                and abs(float(field["area"]) - requirements.field_area) > 0.0005:
            warnings.append("Field area differs between GetFieldDetails and GetCropResourceRequirements; review the field record.")

        resource_checks = [self._check_stock(stock, rules) for stock in stocks]
        low_stocks = low_stock_rows if low_stock_rows is not None else [
            stock for stock in stocks if stock.available_quantity <= stock.low_stock_threshold
        ]
        if not stocks:
            warnings.append("No active inventory rows were available in the resource snapshot.")
        if low_stocks:
            warnings.append(f"{len(low_stocks)} resource item(s) are at or below their low-stock threshold.")

        recommendations = self._weather_recommendations(risk)
        recommendations.extend(
            f"Restock {stock.resource_name}: only {self._format_quantity(stock.available_quantity)} {stock.unit} available."
            for stock in low_stocks
        )
        recommendations.extend(
            f"Obtain at least {self._format_quantity(item.shortage_quantity or 0)} {item.unit} more {item.resource_name}, "
            "or revise the crop plan, before scheduling."
            for item in assessments if item.requirement_status == INSUFFICIENT
        )
        if requirement_status == REQUIREMENT_UNKNOWN and requirements is not None:
            recommendations.append(
                f"Record a verified ResourceRequirement rule for {requirements.crop_name} in the crop reference data "
                "before relying on resource sufficiency."
            )
        if not recommendations:
            recommendations.append("Continue monitoring the stored forecast and inventory snapshot before scheduling.")

        requires_human_review = (
            risk in {"High", "Unknown"}
            or bool(low_stocks)
            or not stocks
            or requirement_status != SUFFICIENT
            or request.field_priority.casefold() in {"high", "unknown"}
        )

        return WeatherResourceOutput(
            workflowId=str(request.workflow_id),
            status="Analyzed",
            requiresHumanReview=requires_human_review,
            warnings=list(dict.fromkeys(warnings)),
            weatherRisk=risk,
            weatherSummary=weather_summary,
            resourceChecks=resource_checks,
            recommendations=recommendations,
            resourceRequirements=assessments,
            requirementStatus=requirement_status,
            requirementSource=requirements.source if requirements else None,
            reason=self._reason(requirement_status, assessments, requirements, risk),
            toolsUsed=tools_used,
        )

    @staticmethod
    async def _call(tools_used: list[str], warnings: list[str], name: str, call: Awaitable[T]) -> T | None:
        try:
            result = await call
        except (ToolClientError, ValueError) as exc:
            warnings.append(f"{name} failed: {exc}")
            return None
        tools_used.append(name)
        return result

    @staticmethod
    def _required_resource_ids(rules: list[CalculatedResourceRequirement]) -> list[UUID]:
        ids = [
            rule.resource_id
            for rule in rules
            if rule.status == "Calculated" and rule.resource_match == "Matched" and rule.resource_id is not None
        ]
        return list(dict.fromkeys(ids))

    @staticmethod
    def _reservations_by_resource(reservations: list[ReservationSnapshot]) -> dict[UUID, tuple[int, float]]:
        totals: dict[UUID, tuple[int, float]] = {}
        for reservation in reservations:
            count, quantity = totals.get(reservation.resource_id, (0, 0.0))
            totals[reservation.resource_id] = (count + 1, quantity + reservation.quantity)
        return totals

    @classmethod
    def _assess(
        cls,
        rule: CalculatedResourceRequirement,
        stocks_by_resource: dict[UUID, StockSnapshot],
        checked_ids: set[UUID],
        reserved_by_resource: dict[UUID, tuple[int, float]],
    ) -> ResourceRequirementAssessment:
        def make(available=None, reserved=None, shortage=None, sufficient=None, status=REQUIREMENT_UNKNOWN, reason=None):
            return ResourceRequirementAssessment(
                ruleId=rule.rule_id, resourceId=rule.resource_id, resourceName=rule.resource_name, unit=rule.resource_unit,
                requiredQuantity=rule.required_quantity, availableQuantity=available, reservedQuantity=reserved,
                shortageQuantity=shortage, sufficient=sufficient, requirementStatus=status, basis=rule.basis,
                reason=reason if reason is not None else rule.reason,
            )

        required = rule.required_quantity
        if rule.status != "Calculated" or required is None:
            return make(reason=rule.reason or "No verified requirement could be calculated.")
        if rule.resource_match == "NotInCatalogue":
            return make(0, 0, required, False, INSUFFICIENT, "No active inventory resource matches this verified requirement.")
        if rule.resource_match != "Matched" or rule.resource_id is None:
            return make(status=NOT_COMPARABLE, reason="More than one active inventory resource matches this requirement.")

        stock = stocks_by_resource.get(rule.resource_id)
        if stock is None:
            # The availability tool was asked for this resource, so a missing row means no active stock record.
            available_reason = "No active stock record exists for this resource."
            if rule.resource_id not in checked_ids:
                available_reason = "Availability was not checked for this resource."
            return make(0, 0, required, False, INSUFFICIENT, available_reason)
        if not cls._units_match(rule.resource_unit, stock.unit):
            return make(stock.available_quantity, stock.reserved_quantity, status=NOT_COMPARABLE,
                        reason=f"Verified requirement is in {rule.resource_unit}; inventory is recorded in {stock.unit}.")

        sufficient = stock.available_quantity >= required
        shortage = round(max(0.0, required - stock.available_quantity), 3)
        count, reserved_total = reserved_by_resource.get(rule.resource_id, (0, 0.0))
        reason = (
            f"{cls._format_quantity(stock.reserved_quantity)} {stock.unit} of {cls._format_quantity(stock.quantity_on_hand)} {stock.unit} "
            f"on hand is already reserved ({count} active reservation(s) totalling {cls._format_quantity(reserved_total)} {stock.unit}); "
            "availability is after reservations."
        )
        return make(stock.available_quantity, stock.reserved_quantity, shortage, sufficient,
                    SUFFICIENT if sufficient else INSUFFICIENT, reason)

    @staticmethod
    def _overall_status(requirements: CropResourceRequirements | None, assessments: list[ResourceRequirementAssessment]) -> str:
        if requirements is None or not any(rule.status == "Calculated" for rule in requirements.requirements):
            return REQUIREMENT_UNKNOWN
        if any(item.requirement_status == INSUFFICIENT for item in assessments):
            return INSUFFICIENT
        if all(item.requirement_status == SUFFICIENT for item in assessments):
            return SUFFICIENT
        return INCOMPLETE

    @classmethod
    def _check_stock(cls, stock: StockSnapshot, rules: list[CalculatedResourceRequirement]) -> ResourceCheck:
        rule = next(
            (item for item in rules
             if item.status == "Calculated" and item.resource_match == "Matched" and item.resource_id == stock.resource_id),
            None,
        )
        if rule is None or rule.required_quantity is None:
            requested, sufficient, status = None, None, REQUIREMENT_UNKNOWN
        elif not cls._units_match(rule.resource_unit, stock.unit):
            requested, sufficient, status = None, None, NOT_COMPARABLE
        else:
            requested = rule.required_quantity
            sufficient = stock.available_quantity >= requested
            status = SUFFICIENT if sufficient else INSUFFICIENT
        return ResourceCheck(
            inventoryStockId=stock.inventory_stock_id,
            resourceId=stock.resource_id,
            resourceName=stock.resource_name,
            unit=stock.unit,
            availableQuantity=stock.available_quantity,
            isLowStock=stock.available_quantity <= stock.low_stock_threshold,
            requested=requested,
            sufficient=sufficient,
            requirementStatus=status,
        )

    @classmethod
    def _requirement_warnings(
        cls, assessments: list[ResourceRequirementAssessment], stocks_by_resource: dict[UUID, StockSnapshot]
    ) -> list[str]:
        warnings = []
        for item in assessments:
            if item.rule_id is None:
                continue
            required = f"{cls._format_quantity(item.required_quantity or 0)} {item.unit}"
            if item.requirement_status == INSUFFICIENT:
                warnings.append(
                    f"{item.resource_name}: {required} required ({item.basis}); "
                    f"{cls._format_quantity(item.available_quantity or 0)} {item.unit} available after reservations; "
                    f"shortage {cls._format_quantity(item.shortage_quantity or 0)} {item.unit}."
                )
            elif item.requirement_status == NOT_COMPARABLE:
                warnings.append(f"{item.resource_name}: {item.reason} Quantities were not compared.")
            elif item.requirement_status == REQUIREMENT_UNKNOWN:
                warnings.append(f"{item.resource_name}: {REQUIREMENT_UNKNOWN}. {item.reason}")
        return warnings

    @classmethod
    def _reason(
        cls,
        requirement_status: str,
        assessments: list[ResourceRequirementAssessment],
        requirements: CropResourceRequirements | None,
        risk: str,
    ) -> str:
        if requirement_status == INSUFFICIENT:
            short = ", ".join(
                f"{item.resource_name} (shortage {cls._format_quantity(item.shortage_quantity or 0)} {item.unit})"
                for item in assessments if item.requirement_status == INSUFFICIENT
            )
            text = f"Required resource quantity exceeds currently available inventory: {short}."
        elif requirement_status == SUFFICIENT:
            text = "Verified resource requirements are covered by inventory available after reservations."
        elif requirement_status == INCOMPLETE:
            text = "Some verified resource requirements could not be determined or compared with inventory."
        else:
            text = ((requirements.reason if requirements else None) or "No verified crop-resource requirement is available.") \
                + " Resource sufficiency was not determined."
        if risk in {"High", "Medium"}:
            text += f" Weather risk is {risk}."
        elif risk == "Unknown":
            text += " Weather risk is Unknown because no forecast was available."
        return text

    @staticmethod
    def _analyze_weather(weather: WeatherForecast, location: str) -> tuple[str, str, list[str]]:
        if not weather.is_available or not weather.days:
            message = weather.message.strip() or "No forecast was returned by the weather provider."
            return (
                "Unknown",
                f"Forecast for {location or weather.location} is unavailable.",
                [f"Weather risk could not be calculated: {message}"],
            )

        max_daily_rain = max(day.rain_mm for day in weather.days)
        total_rain = sum(day.rain_mm for day in weather.days)
        max_temperature = max(day.max_temperature_c for day in weather.days)
        max_wind = max(day.max_wind_speed_ms for day in weather.days)

        if max_daily_rain >= 30 or total_rain >= 80 or max_temperature >= 38 or max_wind >= 15:
            risk = "High"
        elif max_daily_rain >= 10 or total_rain >= 30 or max_temperature >= 34 or max_wind >= 10:
            risk = "Medium"
        else:
            risk = "Low"

        first_day = min(day.date for day in weather.days)
        last_day = max(day.date for day in weather.days)
        summary = (
            f"Forecast for {location or weather.location} from {first_day.isoformat()} to {last_day.isoformat()}: "
            f"{risk} weather risk (maximum daily rain {WeatherResourceAgent._format_quantity(max_daily_rain)} mm, "
            f"total rain {WeatherResourceAgent._format_quantity(total_rain)} mm, "
            f"maximum temperature {WeatherResourceAgent._format_quantity(max_temperature)} C, "
            f"maximum wind {WeatherResourceAgent._format_quantity(max_wind)} m/s)."
        )
        return risk, summary, []

    @staticmethod
    def _weather_recommendations(risk: str) -> list[str]:
        if risk == "High":
            return ["An agricultural officer should review weather hazards before scheduling field work."]
        if risk == "Medium":
            return ["Review the forecast again before confirming weather-sensitive tasks."]
        if risk == "Unknown":
            return ["Obtain a current forecast before confirming weather-sensitive tasks."]
        return []

    @staticmethod
    def _units_match(left: str | None, right: str | None) -> bool:
        return bool(left and left.strip()) and (left or "").strip().casefold() == (right or "").strip().casefold()

    @staticmethod
    def _safe_failure(request: WeatherResourceInput, tools_used: list[str], warning: str) -> WeatherResourceOutput:
        return WeatherResourceOutput(
            workflowId=str(request.workflow_id),
            status="SafeFailure",
            requiresHumanReview=True,
            warnings=[warning],
            weatherRisk="Unknown",
            weatherSummary="",
            resourceChecks=[],
            recommendations=[],
            resourceRequirements=[],
            requirementStatus=REQUIREMENT_UNKNOWN,
            reason=warning,
            toolsUsed=tools_used,
        )

    @staticmethod
    def _format_quantity(value: float) -> str:
        return f"{value:g}"
