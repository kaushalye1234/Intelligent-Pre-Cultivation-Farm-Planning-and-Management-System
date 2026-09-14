# Member 3 - Resources, Weather and WeatherResourceAgent

Member 3 owns resources, inventory, reservations, the weather forecast, and the
`WeatherResourceAgent` step of the crop planning workflow. It adds **no database schema changes**:
everything is built on the existing `Resource`, `InventoryStock`, `StockTransaction`,
`ResourceReservation` and `AgentWorkflow` tables.

## Workflow position

```
Coordinator (Member 1) -> CropFieldAnalysisAgent (Member 2) -> WeatherResourceAgent (Member 3) -> SchedulingValidationAgent (Member 4)
```

When Member 2 completes, the workflow's `CurrentStep` is `WeatherResourceAgent`. Member 3 then runs:

| Method | Route | Roles |
| --- | --- | --- |
| POST | `/api/crop-plans/{id}/run-weather-resource-analysis` | Admin, AgriculturalOfficer, ResourceOfficer |
| GET | `/api/crop-plans/{id}/weather-resource-result` | Anyone who can see the crop plan request |

On success the step is `Completed`, the workflow is `Pending` and `CurrentStep` is
`SchedulingValidationAgent`. On failure the step is `Failed` and the workflow ends as `SafeFailure`
(same behaviour as Member 2).

## How the step works

1. ASP.NET reads Member 2's handoff (`GetMember3HandoffAsync`), the farm location, the weather
   forecast (`IWeatherService`) and a snapshot of up to 100 active inventory rows.
2. It sends all of that to the AI service at `POST /workflows/crop-planning/weather-resource`.
   The agent needs no backend tool calls.
3. The agent computes the weather risk and low-stock flags with fixed rules. If an LLM is
   configured it may only rewrite the summary and recommendation text.
4. ASP.NET validates the output before saving it: the workflow ID must match, only stock IDs from
   the snapshot may appear, available quantities must equal the snapshot, and a weather risk other
   than `Unknown` is rejected when no forecast was available.

Weather risk rules: heavy rain (30 mm in a day or 80 mm total), heat (38 C or more) or wind (15 m/s
or more) is `High`; moderate rain (10 mm/day or 30 mm total), 34 C or more, or 10 m/s or more is
`Medium`; otherwise `Low`. No forecast means `Unknown`.

## Output read by Member 4

```json
{
  "workflowId": "guid",
  "status": "Analyzed | SafeFailure",
  "requiresHumanReview": true,
  "warnings": ["..."],
  "weatherRisk": "Low | Medium | High | Unknown",
  "weatherSummary": "Forecast for Kurunegala from 2026-09-15 to 2026-09-19: Medium weather risk (...)",
  "resourceChecks": [
    { "inventoryStockId": "guid", "resourceId": "guid", "resourceName": "Paddy Seed", "unit": "kg", "availableQuantity": 6, "isLowStock": true }
  ],
  "recommendations": ["Restock Paddy Seed: only 6 kg available."]
}
```

Member 3 never reserves stock. The quantities are a snapshot, so Member 4 must reserve through
`POST /api/resources/reservations`, which re-checks availability and returns `409` with
`INSUFFICIENT_STOCK` or `STOCK_CHANGED` if stock moved in the meantime.

## Resource API

| Method | Route | Notes |
| --- | --- | --- |
| GET/POST | `/api/resources/categories`, `/api/resources/suppliers`, `/api/resources` | Writes: ResourceOfficer, Admin |
| GET/POST | `/api/resources/stocks` | `lowStockOnly=true` filter. Quantity changes are written to the stock ledger |
| GET | `/api/resources/stocks/{id}/history` | Stock ledger |
| GET/POST | `/api/resources/reservations` | Farmers only see their own reservations |
| POST | `/api/resources/reservations/{id}/release`, `/cancel` | Requester, ResourceOfficer or Admin |
| GET | `/api/weather/forecast?location=Kurunegala` | 5-day forecast, never invented |

Concurrency: `InventoryStock.RowVersion` changes on every stock write, so two users changing the
same stock at the same time get `409 STOCK_CHANGED` instead of a silent overwrite.

## Configuration

```ini
Weather__BaseUrl=https://api.openweathermap.org/data/2.5
Weather__ApiKey=<OpenWeatherMap key>
```

If the key is missing or the provider fails, the forecast comes back with `isAvailable: false` and
the agent reports `weatherRisk: "Unknown"`.
