# Member 3 - Resources, Weather and WeatherResourceAgent

Member 3 owns resources, inventory, reservations, the weather forecast, and the
`WeatherResourceAgent` step of the crop planning workflow. It adds **no database schema changes**:
everything is built on the existing `Resource`, `InventoryStock`, `StockTransaction`,
`ResourceReservation`, `AgentWorkflow`, `AgentToolExecution` and verified `CropReferenceProfile` /
`CropRuleReference` tables.

## Workflow position

```
Coordinator (Member 1) -> CropFieldAnalysisAgent (Member 2) -> WeatherResourceAgent (Member 3) -> SchedulingValidationAgent (Member 4)
```

When Member 2 completes, the workflow's `CurrentStep` is `WeatherResourceAgent`. Member 3 then runs:

| Method | Route | Roles |
| --- | --- | --- |
| POST | `/api/crop-plans/{id}/run-weather-resource-analysis` | Admin, AgriculturalOfficer, ResourceOfficer |
| GET | `/api/crop-plans/{id}/weather-resource-result` | Anyone who can see the crop plan request |
| GET | `/api/crop-plans/{id}/member-3-handoff` | FieldOfficer, ResourceOfficer, AgriculturalOfficer, Admin; exact completed Member 2 workflow only |

On success the step is `Completed`, the workflow is `Pending` and `CurrentStep` is
`SchedulingValidationAgent`. On failure the step is `Failed` and the workflow ends as `SafeFailure`
(same behaviour as Member 2).

## How the step works

1. ASP.NET reads Member 2's safe completed handoff (`GetMember3HandoffAsync`) and sends the crop plan context
   (`workflowId`, `agentStepId`, `cropPlanRequestId`, `fieldId`, `location`, dates, `fieldPriority`,
   `fieldAnalysisSummary`, `member2FieldAnalysisContext`) to the AI service at
   `POST /workflows/crop-planning/weather-resource`.
2. The agent gathers its evidence through read-only internal agent tools (see "Agent tools"). Every call carries
   `workflowId` and `agentStepId`; the backend scopes it to the workflow and records it as an `AgentToolExecution`.
3. The agent reasons over the tool results with fixed rules: weather risk, low stock, and required vs. available
   quantity for each verified requirement. No LLM supplies any quantity, rate, stock figure or forecast value.
4. ASP.NET validates the output against the tool results **it recorded for this step** before saving it:
   stock figures and low-stock flags must equal the `GetResourceAvailability` rows, requirement quantities must
   equal the `GetCropResourceRequirements` result, every calculated requirement must be reported exactly once,
   shortage/sufficiency/status must follow arithmetically, a weather risk other than `Unknown` is rejected when no
   forecast was retrieved, and anything short of confirmed `Sufficient` must require human review. Any violation
   turns the result into `SafeFailure`.

Member 3 never reserves, releases or changes stock, and never approves anything.

`WeatherResourceInput` preserves the legacy `fieldPriority` and `fieldAnalysisSummary` fields and adds optional read-only `member2FieldAnalysisContext`:

```json
{
  "fieldSuitability": "SuitableWithConditions",
  "soilAssessment": "Soil type Loamy; condition Moderate; moisture Moist.",
  "waterAssessment": "Water availability Adequate; main source Canal; irrigation Available; reliability Reliable.",
  "drainageAssessment": "Drainage condition Poor; waterlogging risk Moderate.",
  "fieldPreparationRequirements": ["Clear the recorded drainage channels before planting."],
  "plantingReadiness": "RequiresPreparation",
  "identifiedRisks": ["PoorDrainage"],
  "recommendedPrePlantingActions": ["Address the recorded drainage concern before planting."],
  "priority": "High",
  "warnings": [],
  "requiresHumanReview": true
}
```

This is completed structured Member 2 output, not raw evidence. It excludes staff notes, raw observation rows, image metadata, inspection/evidence IDs, crop-issue IDs, and unrelated history. Resource Officers do not manually re-enter Member 2 water, irrigation, drainage, waterlogging, readiness, or risk observations. Pumps, pipes, tanks, and irrigation equipment remain ordinary Member 3 inventory resources and are not substitutes for these Field Officer observations. `WeatherResourceAgent` receives the context read-only; its existing weather and inventory reasoning is unchanged.

Weather risk rules: heavy rain (30 mm in a day or 80 mm total), heat (38 C or more) or wind (15 m/s
or more) is `High`; moderate rain (10 mm/day or 30 mm total), 34 C or more, or 10 m/s or more is
`Medium`; otherwise `Low`. No forecast means `Unknown`.

## Output read by Member 4

The top-level `status` stays `Analyzed | SafeFailure` (`SchedulingValidationAgent` requires `Analyzed`). The fields
below `recommendations` are additive; older stored outputs without them still deserialize.

```json
{
  "workflowId": "guid",
  "status": "Analyzed | SafeFailure",
  "requiresHumanReview": true,
  "warnings": ["Urea: 50 kg required (100 kg/acre x 0.5 acre = 50 kg); 30 kg available after reservations; shortage 20 kg."],
  "weatherRisk": "Low | Medium | High | Unknown",
  "weatherSummary": "Forecast for Kurunegala from 2026-09-27 to 2026-09-28: Medium weather risk (...)",
  "resourceChecks": [
    {
      "inventoryStockId": "guid", "resourceId": "guid", "resourceName": "Urea", "unit": "kg",
      "availableQuantity": 30, "isLowStock": false,
      "requested": 50, "sufficient": false, "requirementStatus": "Insufficient"
    }
  ],
  "recommendations": ["Obtain at least 20 kg more Urea, or revise the crop plan, before scheduling."],
  "resourceRequirements": [
    {
      "ruleId": "guid", "resourceId": "guid", "resourceName": "Urea", "unit": "kg",
      "requiredQuantity": 50, "availableQuantity": 30, "reservedQuantity": 40, "shortageQuantity": 20,
      "sufficient": false, "requirementStatus": "Insufficient",
      "basis": "100 kg/acre x 0.5 acre = 50 kg",
      "reason": "40 kg of 70 kg on hand is already reserved (1 active reservation(s) totalling 40 kg); availability is after reservations."
    }
  ],
  "requirementStatus": "Sufficient | Insufficient | ResourceRequirementUnknown | Incomplete",
  "requirementSource": { "cropReferenceProfileId": "guid", "sourceName": "...", "sourceUrl": "...", "sourceVersion": "...", "verifiedAt": "...", "region": null, "varietyName": null },
  "reason": "Required resource quantity exceeds currently available inventory: Urea (shortage 20 kg). Weather risk is Medium.",
  "toolsUsed": ["GetCropResourceRequirements", "GetFieldDetails", "GetResourceAvailability", "GetExistingReservations", "GetLowStockStatus", "GetWeatherForecast"]
}
```

(Values above are the SAMPLE worked example, not agronomic advice.)

### Verified resource requirements

Requirement rates come **only** from verified crop reference data. They are never estimated, predicted or
produced by an LLM. A rate is a `CropRuleReference` with `RuleType` `ResourceRequirement` on an active
`CropReferenceProfile` (the same verified, source-attributed profiles Member 1 uses). Its `StructuredValueJson` is:

```json
{"resourceName": "Urea", "quantityPerArea": 100, "resourceUnit": "kg", "areaUnit": "acre"}
```

* `resourceName` matches an active inventory resource by name (case-insensitive); `resourceId` may be given instead to pin one.
* `quantityPerArea` > 0; `resourceUnit` must equal the inventory resource's `unit` to be compared; `areaUnit` is `acre` or `hectare`.
* The Admin enters these through the existing crop reference profile screen (Admin > Crop management > reference
  profile > Structured rules), with the source name, URL, version and verification date. The profile validator
  rejects malformed `ResourceRequirement` values and two rules for the same resource.

`GetCropResourceRequirements` selects the profile for the crop plan's crop type (active, not deleted, verified in the
past, containing requirement rules), preferring a variety match, then a region matching the farm location, then the
newest verification. It then calculates, deterministically in the backend:

```
requiredQuantity = quantityPerArea x fieldArea   (converted with 1 acre = 0.40468564224 ha when units differ)
```

`Field.Area` is interpreted in the unit set by `Resources:FieldAreaUnit` (`acre` in `appsettings.json`).

### Requirement statuses

| `requirementStatus` | Meaning |
| --- | --- |
| `Sufficient` | `availableQuantity` (on hand minus reserved) >= `requiredQuantity` |
| `Insufficient` | available < required; `shortageQuantity` = required - available. A resource missing from inventory counts as 0 available |
| `ResourceRequirementUnknown` | No verified rule, no field, field area not recorded, area unit not configured, invalid or duplicate rule. `requiredQuantity` is `null`; nothing is guessed |
| `InventoryNotComparable` | Requirement known but the rule unit differs from the inventory unit, or several resources match the name |
| `Incomplete` | Overall only: some requirements assessed, others unknown or not comparable |

Anything other than an overall `Sufficient` sets `requiresHumanReview`. `resourceChecks[].requested/sufficient/requirementStatus`
carry the same figures per stock row for older consumers.

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
Resources__FieldAreaUnit=acre        # unit of Field.Area; default in appsettings.json
AI__ToolToken=<shared token>         # ASP.NET side of the internal agent tools
# ai-service: BACKEND_TOOL_BASE_URL=<ASP.NET base URL>, BACKEND_TOOL_TOKEN=<same token>
```

If the weather key is missing or the provider fails, the forecast comes back with `isAvailable: false` and
the agent reports `weatherRisk: "Unknown"`. If `Resources:FieldAreaUnit` is missing or not `acre`/`hectare`, every
requirement is `ResourceRequirementUnknown`. If the tool token is not configured, the agent returns `SafeFailure`.

## Agent tools

All tools are read-only `GET` handlers under `/api/internal/agent-tools`, authenticated with `AI:ToolToken`, scoped
to the workflow (`workflowId` is required), and logged as `AgentToolExecution` rows on the Member 3 step.

| Tool | Route | Returns |
| --- | --- | --- |
| `GetCropResourceRequirements` | `crop-resource-requirements/{cropPlanRequestId}` | Crop, variety, field area and unit, source profile, and each verified rule with `requiredQuantity` calculated by the backend (or `null` with a reason) |
| `GetFieldDetails` | `fields/{fieldId}` (existing Member 2 tool) | Field name, area, soil type |
| `GetResourceAvailability` | `resource-availability?resourceIds=...` | Stock rows for the requested resources plus the rest of the inventory, up to 100 rows: on hand, reserved, available, low-stock threshold |
| `GetExistingReservations` | `existing-reservations?resourceIds=...` | Active reservation rows (quantity, purpose, created at) |
| `GetLowStockStatus` | `low-stock-status` | Rows at or below their low-stock threshold |
| `GetWeatherForecast` | `weather-forecast` | Daily forecast for the crop plan's farm location (`isAvailable: false` when the provider fails, never invented) |

`GetResourceAvailability` is mandatory: if it fails the agent returns `SafeFailure`. Other tool failures are reported
as warnings, and the affected part becomes `Unknown`. No tool can reserve, release, approve or write inventory.
