from providers.base_llm_provider import BaseLLMProvider
from schemas.weather_resource import ResourceCheck, WeatherResourceInput, WeatherResourceOutput


class WeatherResourceAgent:
    """Deterministic weather and inventory analysis for the Member 3 workflow step."""

    def __init__(
        self,
        llm_provider: BaseLLMProvider | None = None,
        provider_timeout_seconds: float = 30,
    ) -> None:
        # The contract permits an LLM to rewrite text, but the workflow does not need one.
        # Keeping facts and recommendations deterministic prevents invented weather or stock data.
        self._llm_provider = llm_provider
        self._provider_timeout_seconds = provider_timeout_seconds

    async def run(self, request: WeatherResourceInput) -> WeatherResourceOutput:
        risk, weather_summary, weather_warnings = self._analyze_weather(request)
        resource_checks = [self._check_stock(stock) for stock in request.stocks]
        low_stocks = [check for check in resource_checks if check.is_low_stock]

        warnings = list(weather_warnings)
        if not request.stocks:
            warnings.append("No active inventory rows were available in the resource snapshot.")
        if low_stocks:
            warnings.append(f"{len(low_stocks)} resource item(s) are at or below their low-stock threshold.")

        recommendations = self._weather_recommendations(risk)
        recommendations.extend(
            f"Restock {stock.resource_name}: only {self._format_quantity(stock.available_quantity)} {stock.unit} available."
            for stock in low_stocks
        )
        if not recommendations:
            recommendations.append("Continue monitoring the stored forecast and inventory snapshot before scheduling.")

        requires_human_review = (
            risk in {"High", "Unknown"}
            or bool(low_stocks)
            or not request.stocks
            or request.field_priority.casefold() in {"high", "unknown"}
        )

        return WeatherResourceOutput(
            workflowId=str(request.workflow_id),
            status="Analyzed",
            requiresHumanReview=requires_human_review,
            warnings=warnings,
            weatherRisk=risk,
            weatherSummary=weather_summary,
            resourceChecks=resource_checks,
            recommendations=recommendations,
        )

    @staticmethod
    def _analyze_weather(request: WeatherResourceInput) -> tuple[str, str, list[str]]:
        weather = request.weather
        if not weather.is_available or not weather.days:
            message = weather.message.strip() or "No forecast was returned by the weather provider."
            return (
                "Unknown",
                f"Forecast for {request.location or weather.location} is unavailable.",
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
        location = request.location or weather.location
        summary = (
            f"Forecast for {location} from {first_day.isoformat()} to {last_day.isoformat()}: "
            f"{risk} weather risk (maximum daily rain {WeatherResourceAgent._format_quantity(max_daily_rain)} mm, "
            f"total rain {WeatherResourceAgent._format_quantity(total_rain)} mm, "
            f"maximum temperature {WeatherResourceAgent._format_quantity(max_temperature)} C, "
            f"maximum wind {WeatherResourceAgent._format_quantity(max_wind)} m/s)."
        )
        return risk, summary, []

    @staticmethod
    def _check_stock(stock) -> ResourceCheck:
        return ResourceCheck(
            inventoryStockId=stock.inventory_stock_id,
            resourceId=stock.resource_id,
            resourceName=stock.resource_name,
            unit=stock.unit,
            availableQuantity=stock.available_quantity,
            isLowStock=stock.available_quantity <= stock.low_stock_threshold,
        )

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
    def _format_quantity(value: float) -> str:
        return f"{value:g}"
