# Member 1 crop planning design (2026-09-24)

## Existing architecture audit

1. Flutter `CropPlanScreen` has farm, field, crop, dates, budget, and objective. Field is optional and unfiltered; crop is a plain dropdown. The page shows only the last workflow.
2. `CropPlanRequest` stores farm, optional field, crop type, dates, budget, objective, and status. `CropPlanningDtos` exposes the same data; the create/update validators bound dates, budget, and objective but do not require a field.
3. `CropType` has name, description, and active state. `CropCycle` links a field and crop to planned dates. Neither is a variety or season catalog.
4. `CropReferenceProfile` stores optional variety name and region, source and verification metadata, active state, stages, and rules. The internal AI tool only returns active profiles and reports `Unavailable` when none match. Variety name on a reference profile does not provide an independent active variety catalog.
5. React `CropPlanningPage` lists crop types but has no crop, variety, or reference editing. Existing crop type writes allow Admin and AgriculturalOfficer; Member 1 management will be Admin only.
6. `/api/crop-planning` exposes farm, field, crop type, crop cycle, and request operations. Farmer access to farms, fields, and requests is scoped by farm ownership. The service currently checks field access but not that the chosen field belongs to the chosen farm.
7. Flutter is already farmer only and its navigation is Dashboard, Plans, My status. `AppState` stores only the last request workflow. The Plans page has no request list or filters.
8. The coordinator receives a typed input with crop, objective, budget, and start date, then fetches crop plan context, farm, field, cycle, verified profile, and history. It returns `Planned`, `ReferenceDataUnavailable`, or `SafeFailure`; the backend creates pending downstream steps and sets `CropFieldAnalysisAgent` as the current step only for `Planned`.
9. Approval is owned by the existing task approval service. Its read endpoints expose workflow review, decisions, tasks, and irrigation schedules to a farmer through ownership checks; no separate final plan entity exists.

## Design choices

Two options were considered for varieties. Deriving choices from reference profile names would avoid a table, but variety activation and multiple regional profiles would be ambiguous. A separate `CropVariety` master record gives Admin a stable active catalog and lets a request reference one variety while keeping verified reference profiles as evidence. This is the selected option. The existing crop type, crop cycle, and reference profile remain in use.

Add optional `CropVarietyId` and `PreviousCropTypeId`, a season value, and a bounded set of previous known problem codes to the request. Require field for new submissions while retaining nullable storage for historical records. Validate farm ownership, field/farm match, active crop and variety, valid season, dates, budget, and objective before AI. Use existing request status values. Preserve backward compatibility in read responses where historical values are absent.

Admin management will create and edit crop types and varieties, including active state. It will list and maintain verified reference profiles and their source metadata without treating an unverified variety as reference evidence. The farmer catalog endpoints return only active crop types and varieties; Admin can request inactive entries. No AI step creates or verifies reference facts.

Flutter Plans will contain a new request form and a list with All, In Progress, Approved, and Rejected filters. It will use backend request status and workflow step status for progress. Approved detail will show only values available in the request and approved workflow review; tasks and irrigation records appear only when linked data is exposed. My status remains operational. No approval action is added to Flutter.

The coordinator input, Python schema, internal context DTO, and reference lookup will carry the selected variety, season, end date, previous crop, and historical problems. A selected variety must resolve to a matching verified profile; missing evidence yields `ReferenceDataUnavailable`. The coordinator keeps its current forbidden-action boundary and deterministic pending Field Analysis handoff.

## Verification

Add focused backend tests for ownership, field/crop/variety validation, persistence, reference selection, and handoff; AI tests for evidence and safe failure; React tests for Admin writes and catalog filtering; Flutter tests for form and lifecycle. Run the repository's backend, AI, React, and Flutter checks. Database migration behavior requires isolated PostgreSQL verification and must not be inferred from EF InMemory tests.
