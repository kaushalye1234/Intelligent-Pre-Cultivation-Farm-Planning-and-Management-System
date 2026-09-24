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
    {
      "inventoryStockId": "guid", "resourceId": "guid", "resourceName": "Paddy Seed", "unit": "kg",
      "availableQuantity": 6, "isLowStock": true,
      "requested": null, "sufficient": null, "requirementStatus": "ResourceRequirementUnknown"
    }
  ],
  "recommendations": ["Restock Paddy Seed: only 6 kg available."]
}
```

### Requested quantity, sufficiency and `ResourceRequirementUnknown`

Each resource check carries three requirement fields:

| Field | Meaning |
| --- | --- |
| `requested` | Quantity crop planning says the plan needs, or `null` when none was stated |
| `sufficient` | `availableQuantity >= requested`, or `null` when `requested` is `null` |
| `requirementStatus` | `Sufficient`, `Insufficient` or `ResourceRequirementUnknown` |

Requirements enter through the optional `resourceRequirements` input list (`resourceId`, `requestedQuantity`).
**Crop planning (Member 1) does not currently provide required quantities**, so ASP.NET sends an empty list and
every check is `ResourceRequirementUnknown` with `requested` and `sufficient` set to `null`. The agent never
estimates a requirement. `ResourceRequirementUnknown` is a per-check status only; the top-level `status` stays
`Analyzed | SafeFailure` because `SchedulingValidationAgent` requires `Analyzed`. An `Insufficient` check, or a
requirement for a resource with no stock row, sets `requiresHumanReview`.

ASP.NET rejects (and converts to `SafeFailure`) any output whose requirement figures are not derivable from the
input: a check with `requested` but no supplied requirement, a `requested` that differs from the supplied one,
or a `sufficient` / `requirementStatus` that does not follow from the snapshot.

Member 3 never reserves stock. The quantities are a snapshot, so Member 4 must reserve through
`POST /api/resources/reservations`, which re-checks availability and returns `409` with
`INSUFFICIENT_STOCK` or `STOCK_CHANGED` if stock moved in the meantime.

## Resource API

| Method | Route | Notes |
| --- | --- | --- |
| GET/POST/PUT/DELETE | `/api/resources/categories`, `/api/resources/suppliers`, `/api/resources` (`PUT` and `DELETE` on `/{id}`) | Reads: any signed-in user. Writes: ResourceOfficer, Admin. Deletes are soft deletes and return `204` |
| GET/POST | `/api/resources/stocks` | `lowStockOnly=true`, `search` (resource name). Rows include `resourceName` and `unit`. Quantity changes are written to the stock ledger |
| GET | `/api/resources/stocks/{id}/history` | Stock ledger |
| GET/POST | `/api/resources/reservations` | Farmers only see their own reservations. Rows include `resourceName`, `unit`, `createdAt` |
| POST | `/api/resources/reservations/{id}/release`, `/cancel` | Requester, ResourceOfficer or Admin |
| GET | `/api/weather/forecast?location=Kurunegala` | 5-day forecast, never invented |

### Paging, sorting and filtering

All list endpoints take `page`, `pageSize` (1-100) and `search`. `sortBy` is case-insensitive and `sortDirection` is
`asc` or `desc`; an unknown `sortBy` falls back to the default field (the direction is still applied). Ties are
broken by `id` so paging is stable.

| Endpoint | `sortBy` values (default first) | Extra filters |
| --- | --- | --- |
| `/api/resources` | `name`, `unit`, `isActive`, `createdAt`, `category` | `categoryId`, `supplierId` (combine with `search`) |
| `/api/resources/categories` | `name`, `createdAt` | |
| `/api/resources/suppliers` | `name`, `email`, `phone`, `createdAt` | |
| `/api/resources/stocks` | `resourceId`, `resourceName`, `quantityOnHand`, `reservedQuantity`, `availableQuantity`, `lowStockThreshold`, `createdAt` | `lowStockOnly` |
| `/api/resources/reservations` | newest first (not sortable) | `status` |

### Delete rules

* A **resource** cannot be deleted while it has active reservations (`409 RESOURCE_HAS_ACTIVE_RESERVATIONS`); its stock row is soft-deleted with it.
* A **category** or **supplier** used by a non-deleted resource cannot be deleted (`409 CATEGORY_IN_USE` / `SUPPLIER_IN_USE`).
* Category names are unique, ignoring case and including soft-deleted rows (`409 CATEGORY_NAME_EXISTS`), because of the existing unique index.

### Stock history

`GET /api/resources/stocks/{id}/history` returns `type` (1 Add, 2 Remove, 3 Reserve, 4 Release), `quantity`, `note` and
`createdAt`. Previous and new quantity are **not** stored, so no client shows them.

Concurrency: `InventoryStock.RowVersion` changes on every stock write, so two users changing the
same stock at the same time get `409 STOCK_CHANGED` instead of a silent overwrite. This is covered by
PostgreSQL tests in `ResourceInventoryPostgreSqlIntegrationTests` (set `AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING`).

## Configuration

```ini
Weather__BaseUrl=https://api.openweathermap.org/data/2.5
Weather__ApiKey=<OpenWeatherMap key>
```

If the key is missing or the provider fails, the forecast comes back with `isAvailable: false` and
the agent reports `weatherRisk: "Unknown"`.

## Agent tools: decision

The specification's six named tools (`GetWeatherForecast`, `GetResourceAvailability`, `GetResourceDetails`,
`GetExistingReservations`, `GetFieldLocation`, `GetLowStockStatus`) are **not implemented as callable tools**.
The repository contains no document requiring them for Member 3 (only the Member 4 plan names
`GetResourceAvailability` and `GetExistingReservations`, for the scheduling agent), and this step was deliberately
built around one pre-assembled payload so the agent needs no backend tool calls (see "How the step works").
Adding tool endpoints would grow the shared internal tool surface while the agent would still have nothing extra to fetch.

The same evidence is provided, read-only, through the input that ASP.NET assembles under the caller's authorization:

| Tool | Where the data comes from today |
| --- | --- |
| `GetWeatherForecast` | `weather` (IWeatherService; `isAvailable: false` when the provider fails, never invented) |
| `GetFieldLocation` | `location` (farm location of the crop plan request) |
| `GetResourceAvailability` | `stocks[].availableQuantity`, `quantityOnHand`, `reservedQuantity` |
| `GetLowStockStatus` | `stocks[].lowStockThreshold` and the derived `isLowStock` |
| `GetResourceDetails` | `stocks[].resourceName` and `unit` only. **Gap:** category and supplier are not in the payload |
| `GetExistingReservations` | Aggregate `reservedQuantity` only. **Gap:** individual reservations are not in the payload |

If the team confirms the tools are required, add them as read-only `GET` handlers under
`/api/internal/agent-tools` (token-authenticated, workflow-scoped, logged as `AgentToolExecution`) like the
Member 1 and Member 2 tools, and keep the ASP.NET output validation unchanged.
