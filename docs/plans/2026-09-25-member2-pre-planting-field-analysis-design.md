# Member 2 Pre-Planting Field Analysis Design

**Date:** 2026-09-25
**Branch:** `Field-Inspection-&-Crop-Issue-Management`
**Scope:** Member 2 only

## Goal

Complete the Field Officer pre-planting assessment and `CropFieldAnalysisAgent` workflow using exact request-linked evidence. A submitted inspection permits analysis but does not complete Member 2; only a successfully validated and persisted field-analysis result may advance the workflow to the existing `WeatherResourceAgent` step.

## Existing Architecture and Gaps

The repository already reuses `FieldInspection`, `InspectionObservation`, `InspectionImage`, and `AgentStep.OutputJson`. `FieldInspection` has `CropPlanRequestId` and `InspectionPurpose`, with `Routine = 1` and `PrePlanting = 2`. EF persists `Purpose` as a string in PostgreSQL through `HasConversion<string>()`.

The current implementation already blocks the backend run endpoint until a linked completed inspection exists and scopes internal inspection, issue, and image tools by crop-plan request, inspection, and field. It sends image metadata rather than image content.

The audit found that missing evidence is incorrectly reported as `Analyzed`, priority is issue-only, the fallback ignores structured observations, crop-plan context is absent, draft validation requires completion, submission does not independently validate persisted observations, generic reads expose raw evidence too broadly, AI failures become terminal, the output is too generic, and the unique index restricts more than PrePlanting assessments.

## Architecture

```text
CropPlanRequest
  -> one linked PrePlanting FieldInspection
    -> typed InspectionObservation rows
    -> InspectionImage metadata
    -> submitted/completed inspection
    -> CropFieldAnalysisAgent
    -> validated AgentStep.OutputJson
    -> WeatherResourceAgent
```

No `PrePlantingAssessment` table and no separate field-analysis result table will be introduced.

## Lifecycle and Completion Boundary

```text
Draft
  -> Saved Draft
  -> Submitted/Completed Inspection
  -> AI Analysis Allowed
  -> CropFieldAnalysisAgent succeeds
  -> Output validates
  -> Output persists
  -> Member 2 step Completed
  -> WeatherResourceAgent
```

Submitting a valid assessment may set `FieldInspection.Status` to `Completed`, but it does not complete the Member 2 agent step or advance the workflow. Only the final successful field-analysis persistence operation may do that.

## Structured Assessment Contract

The nullable property-based draft DTO contains:

- `soilType`, `soilCondition`, `soilMoisture`, `soilNotes`
- `waterAvailability`, `mainWaterSource`, `irrigationAvailability`, `waterReliability`, `waterConcerns`
- `drainageCondition`, `waterloggingRisk`, `drainageNotes`
- `generalFieldCondition`, `generalFieldNotes`
- `plantingReadiness`, `identifiedRisks`, `riskNotes`, `officerNotes`
- `risksAndConcerns` as a legacy input alias

Each populated scalar is stored as one typed `InspectionObservation`. Each risk is stored as an `IdentifiedRisk` observation. Existing `RisksAndConcerns` data is read as `RiskNotes` when no canonical value exists.

### Allowed Values

| Field | Values |
|---|---|
| Soil type | `Sandy`, `Clay`, `Loamy`, `Silty`, `Mixed`, `Unknown`, `Other` |
| Soil condition | `Good`, `Moderate`, `Poor`, `Compacted`, `Eroded`, `Unknown`, `Other` |
| Soil moisture | `Dry`, `Moist`, `Wet`, `Waterlogged`, `Unknown` |
| Water availability | `Adequate`, `Limited`, `Unavailable`, `Seasonal`, `Unknown` |
| Irrigation availability | `Available`, `Limited`, `Unavailable`, `NotRequired`, `Unknown` |
| Water reliability | `Reliable`, `Intermittent`, `Seasonal`, `Unreliable`, `Unknown` |
| Drainage condition | `Good`, `Moderate`, `Poor`, `Unknown` |
| Waterlogging risk | `NoneObserved`, `Low`, `Moderate`, `High`, `Unknown` |
| General field condition | `ClearAndPrepared`, `RequiresLandPreparation`, `UnevenField`, `Waterlogged`, `TooDry`, `ErosionPresent`, `AccessLimitation`, `Other` |
| Planting readiness | `Ready`, `ReadyWithMinorPreparation`, `RequiresPreparation`, `NotReady`, `RequiresFurtherAssessment` |
| Identified risk | `WaterShortageRisk`, `FloodingRisk`, `PoorDrainage`, `SoilSuitabilityConcern`, `SoilErosion`, `FieldAccessProblem`, `LandPreparationRequired`, `Other` |

`identifiedRisks` preserves three meanings: `null` means not assessed, `[]` means assessed with none identified, and a non-empty list contains identified risks. Draft persistence never normalizes `null` to `[]`. Submission requires a non-null list and rejects duplicates.

Persistence uses an `IdentifiedRisksAssessment = Assessed` marker observation whenever the list is non-null. The marker with no `IdentifiedRisk` rows represents an explicit empty list; absence of the marker represents null.

## Draft and Submission Validation

Draft save allows every observation to be absent. It validates exact request/farm/field linkage, the active Member 2 stage, owning Field Officer, supplied value formats, list uniqueness, and text lengths. It keeps the inspection `InProgress` and never runs AI or advances the workflow. A submitted assessment cannot be overwritten.

Submission reloads the persisted inspection, observations, images, request, and workflow. It requires all structured fields except notes, plus a non-null risk list. `MainWaterSource` is required only for `Adequate`, `Limited`, or `Seasonal` water. Conditional notes are required for Other soil/general/risk values, limited/unavailable/seasonal water, poor drainage, and moderate/high waterlogging risk. Officer notes and images remain optional. Every image must belong to the exact inspection.

Repeated valid submission is idempotent. Generic inspection submission cannot submit a PrePlanting assessment and bypass these rules.

## Immutability and Context

After submission, generic update, observation, evidence, close, and submission paths cannot mutate the assessment. Reopening requires a future explicit revision workflow.

`GET /api/crop-plans/{id}/pre-planting-context` returns Farmer, Farm, Field, Crop, Variety, Cultivation Season, preferred dates, workflow ID, and current step to Field Officer, Agricultural Officer, and Admin. The existing `FieldAnalysisInput` stays unchanged; the Python agent calls the scoped crop-plan-context tool.

## AI Evidence Rules

Primary evidence is the exact submitted PrePlanting inspection, its structured observations, and its image metadata. Secondary context is limited to exact linked open issues, crop-plan historical values such as `previousKnownProblems`, and verified crop reference data.

The agent does not query a generic latest inspection or broad unrelated history. Officer and Farmer text is evidence data, not instructions. Image metadata may indicate that human-review evidence exists but must never be treated as visual findings.

Missing inspection, invalid required observations, unavailable required reference data, tool failures, provider failures, malformed output, unknown evidence IDs, or unsafe output return `SafeFailure` with `requiresHumanReview = true`.

## Structured Field-Analysis Output

The existing `fieldCondition`, `openIssues`, and `priority` fields remain. The output gains:

- `fieldSuitability`
- `soilAssessment`
- `waterAssessment`
- `drainageAssessment`
- `fieldPreparationRequirements`
- `plantingReadiness`
- `identifiedRisks`
- `recommendedPrePlantingActions`

Field suitability values are `Suitable`, `SuitableWithConditions`, `NotSuitable`, `RequiresFurtherAssessment`, and `Unknown`. Structured readiness uses the assessment readiness codes plus `Unknown`.

Deterministic output summarizes actual stored observations, uses only supported facts, never invents image findings, and never suggests unauthorized chemical treatment or final approval.

## Deterministic Priority

Current PrePlanting observations establish the base priority.

High:

- `PlantingReadiness = NotReady`
- `WaterAvailability = Unavailable`
- `WaterloggingRisk = High`
- `GeneralFieldCondition = Waterlogged`
- A severe supported soil/risk condition
- An exact linked issue may raise priority to High

Medium:

- `PlantingReadiness = RequiresPreparation`
- `PlantingReadiness = RequiresFurtherAssessment`
- `WaterAvailability = Limited` or `Seasonal`
- `DrainageCondition = Poor`
- `WaterloggingRisk = Moderate`
- Preparation, access, or erosion concerns

Low:

- Readiness is `Ready` or `ReadyWithMinorPreparation`
- Water is `Adequate`
- Drainage is `Good` or `Moderate`
- Waterlogging risk is `NoneObserved` or `Low`
- No material structured risks exist

Unknown is used only when required evidence is missing, invalid, or insufficient. Exact linked issues may raise priority but cannot lower or replace the observation-based priority.

## Member 2 to Member 3 Boundary

Raw Member 2 evidence remains restricted:

- Field Officer: operational access; only the owning officer may mutate.
- Agricultural Officer and Admin: read-only access.
- Resource Officer and Farmer: no raw assessment or evidence access.

Resource Officers cannot read raw inspections, observations, officer notes, risk notes, images, or generic PrePlanting history. They cannot save, upload, submit, run, or retry Member 2 operations.

Member 3 instead receives a safe read-only completed field-analysis summary for the exact crop-plan request after the workflow reaches `WeatherResourceAgent`. The summary may expose field suitability, soil/water/drainage summaries, preparation requirements, readiness, identified risks, recommended actions, priority, warnings, and human-review state. It excludes officer notes, raw observations, images, and unrelated history.

The safe summary is exposed through the Crop Plan/workflow result boundary rather than generic inspection APIs. Resource Officer access is limited to the exact workflow context. Member 3 consumes water and drainage analysis without manual re-entry. Pumps, pipes, tanks, and irrigation equipment remain Member 3 resource/inventory facts and are not substitutes for Member 2 field observations.

## React Experience

The React panel displays crop-plan context and uses structured selects. `identifiedRisks` has explicit Not Assessed, None Identified, and Risks Identified states so UI initialization cannot change `null` into `[]`.

Actions are separate:

1. Save Draft
2. Upload Evidence
3. Submit Assessment
4. Run AI Field Analysis

The UI supports no-assessment, draft, incomplete, submitted, AI-ready, running, failed/retryable, completed, and historical read-only states. Agricultural Officer and Admin never receive mutation controls. Resource Officer and Farmer cannot reach the raw Member 2 panel.

## Role Authorization

| Action | Field Officer | Agricultural Officer/Admin | Resource Officer | Farmer |
|---|---|---|---|---|
| View raw assessment/evidence | Yes | Read-only | No | No |
| Save/upload/submit/run/retry | Owning officer only | No | No | No |
| View full staff result | Yes | Yes | No | No |
| View Member-3-safe summary | Yes | Yes | Exact authorized Member 3 workflow only | No |

## Generic Inspection Access Hardening

Farmer and Resource Officer queries exclude PrePlanting inspections and all dependent observations, issues, images, history, and follow-ups. Field Officer, Agricultural Officer, and Admin retain intended read access. Existing Routine-inspection behavior remains unchanged.

## AI Failure and Retry

A failed analysis keeps the assessment and evidence, stores safe failure audit data, marks the agent step failed and retryable, keeps workflow `CompletedAt` null, keeps `CurrentStep = CropFieldAnalysisAgent`, and does not advance.

Pending and retryable failed steps may run. Running steps reject duplicates. Completed steps return the persisted result without calling AI again.

## Concurrency and Idempotency

The existing `AgentWorkflow.Version` concurrency token protects run acquisition. Starting a run changes the step to Running and increments the workflow version before the external AI call. A request holding an older version cannot start another call.

Final output validation, output persistence, step completion, workflow version update, and transition to `WeatherResourceAgent` occur atomically. Failed output persistence cannot advance the workflow.

Repeated draft saves update one inspection. Repeated valid submission returns the existing completed inspection. The database uniqueness rule prevents duplicate PrePlanting assessments.

## PostgreSQL Partial Unique Index

The verified physical representation is a string-backed `Purpose` column. The broad index is replaced with a PostgreSQL partial unique index equivalent to:

```sql
CREATE UNIQUE INDEX ...
ON FieldInspections (CropPlanRequestId)
WHERE CropPlanRequestId IS NOT NULL
  AND Purpose = 'PrePlanting';
```

This enforces one linked PrePlanting assessment per crop-plan request while allowing multiple Routine inspections, including linked Routine inspections. Migration, EF mapping, designer, and model snapshot remain aligned.

## Verification

Backend coverage includes:

- Incomplete draft save and null-versus-empty risks.
- Persisted submission validation and all conditional rules.
- Exact request, field, purpose, evidence, and officer ownership.
- Immutable submitted assessments and idempotent submission.
- Field Officer writes; Agricultural Officer/Admin read-only access.
- Farmer/Resource Officer raw-evidence denial through dedicated and generic APIs.
- Resource Officer access to only the exact completed Member-3-safe summary.
- Routine-inspection regression coverage.
- Missing/draft evidence, retryable failures, successful retry, duplicate runs, completed-run idempotency, unknown IDs, and exact Member 3 transition.
- Isolated PostgreSQL proof that multiple Routine inspections are allowed and duplicate PrePlanting assessments are rejected.

AI coverage includes crop-plan context, exact observations, missing-evidence SafeFailure, verified reference data, deterministic structured output, the priority matrix, prompt injection, metadata-only images, provider failure, and unknown evidence IDs.

React coverage includes context display, nullable risks, incomplete drafts, evidence upload, separate actions, submission errors, role behavior, running/failure/retry/completed states, and structured output.

Final checks run backend restore/build/tests, AI compile/tests, React lint/tests/build, conflict-marker scanning, and `git diff --check`. PostgreSQL-specific behavior is never reported as verified by EF InMemory tests.

## Git Safety

All work remains on `Field-Inspection-&-Crop-Issue-Management`. No branch is created or switched. Each verified step receives a focused local commit. No push, pull request, or remote modification is permitted.
