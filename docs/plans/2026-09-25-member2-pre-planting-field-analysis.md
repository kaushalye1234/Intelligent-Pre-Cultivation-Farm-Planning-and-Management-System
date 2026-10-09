# Member 2 Pre-Planting Field Analysis Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver a role-safe, exact-evidence, structured pre-planting assessment and field-analysis workflow that advances to Member 3 only after validated Member 2 success.

**Architecture:** Extend the existing linked `FieldInspection` design instead of creating duplicate persistence. ASP.NET owns authorization, draft/submission validation, workflow concurrency, persistence, and the safe Member 3 boundary; FastAPI performs evidence-bound analysis; React provides the staff workflow.

**Tech Stack:** ASP.NET Core 8, EF Core 8, PostgreSQL, xUnit, FastAPI, Pydantic, pytest, React 19, TypeScript, Vite, Vitest

---

## Preconditions

- Stay on `Field-Inspection-&-Crop-Issue-Management`; never create or switch branches.
- Never push, modify remotes, or create a pull request.
- Before every commit: inspect status/branch, review the focused diff, run listed checks, and run `git diff --check`.
- After every commit: record hash/message and confirm status.
- Use @dotnet-best-practices, @api-design, @frontend-design, and @vercel-react-best-practices when implementation begins.
- The repository-specific `agriassist-*` and named executing-plans skills are unavailable; follow this plan directly.
- PostgreSQL behavior must be verified with the isolated PostgreSQL suite, not EF InMemory.

## Null-versus-empty risk persistence

```text
No IdentifiedRisksAssessment row
  => identifiedRisks = null

IdentifiedRisksAssessment = Assessed and no IdentifiedRisk rows
  => identifiedRisks = []

IdentifiedRisksAssessment = Assessed plus IdentifiedRisk rows
  => identifiedRisks = [values]
```

The marker and risk rows are replaced together on draft save and become immutable after submission.

Approved design: `docs/plans/2026-09-25-member2-pre-planting-field-analysis-design.md`
Design commit: `ff00fcc docs: document pre-planting field analysis design`

### Task 1: Structured linked assessment lifecycle and PostgreSQL uniqueness

**Files:**
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/CropPlanningWorkflowDtos.cs`
- Modify: `backend/AgriAssist.Api/Validators/CropPlanning/CropPlanningValidators.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/ICropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/CropPlanning/CropPlansWorkflowController.cs`
- Modify: `backend/AgriAssist.Api/Data/AppDbContext.cs`
- Create: `backend/AgriAssist.Api/Migrations/<timestamp>_ScopePrePlantingAssessmentUniqueIndex.cs`
- Create: `backend/AgriAssist.Api/Migrations/<timestamp>_ScopePrePlantingAssessmentUniqueIndex.Designer.cs`
- Modify: `backend/AgriAssist.Api/Migrations/AppDbContextModelSnapshot.cs`
- Modify: `backend/AgriAssist.Api.Tests/ValidatorTests.cs`
- Modify: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`
- Create: `backend/AgriAssist.Api.Tests/PrePlantingAssessmentPostgreSqlIntegrationTests.cs`

**Step 1: Write failing tests**

Add tests for incomplete draft acceptance; supplied-value validation; duplicate risks; null versus empty risks; every submission requirement; conditional main water source/notes; exact request-field-purpose-owner linkage; draft upsert; immutable submission; idempotent submission; context response; and multiple Routine versus one PrePlanting database uniqueness.

Run:

```powershell
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --filter 'FullyQualifiedName~ValidatorTests|FullyQualifiedName~CropPlanningAiWorkflowTests' --no-restore
```

Expected: FAIL because the structured contract and dedicated submission do not exist.

**Step 2: Add nullable typed DTOs and two-level validation**

Define string-serialized enums for the approved SoilType, SoilCondition, SoilMoisture, WaterAvailability, IrrigationAvailability, WaterReliability, DrainageCondition, WaterloggingRisk, GeneralFieldCondition, PlantingReadiness, and Risk values.

Make `PrePlantingAssessmentRequest` property-based and nullable, including:

```csharp
public IReadOnlyList<PrePlantingRisk>? IdentifiedRisks { get; init; }
```

Mirror values in the response, retain existing identity/status/images, and add `InspectorUserId`. Add `PrePlantingContextResponse`.

`PrePlantingAssessmentRequestValidator` validates only supplied enum/list/text integrity. `PrePlantingAssessmentRules.ValidateSubmission` applies all required and conditional rules to a reconstructed persisted DTO.

**Step 3: Implement draft persistence**

- Require active Member 2 stage and Field Officer role.
- Create one exact linked PrePlanting inspection with the actor as `InspectorUserId`.
- Require the same owner for later saves.
- Reject PUT after completion.
- Replace observation rows with only populated scalar values.
- Persist the approved risk marker plus zero/more risk rows without normalizing null.
- Read legacy `RisksAndConcerns` only as a `RiskNotes` fallback.
- Convert a uniqueness race to an existing-owned-draft reload or safe conflict.

**Step 4: Implement context and dedicated submission**

Add service/controller operations for context and `POST /api/crop-plans/{id}/pre-planting-assessment/submit`. Submission reloads the exact inspection, observations, images, request, and workflow; reconstructs the DTO; validates ownership/linkage/required values; validates image linkage; and changes only the inspection to Completed. Repeated valid submission returns the same response.

**Step 5: Replace the unique index**

Change EF mapping to a unique `CropPlanRequestId` index filtered by non-null request ID and the verified string-backed PrePlanting purpose. Generate:

```powershell
dotnet ef migrations add ScopePrePlantingAssessmentUniqueIndex --project backend/AgriAssist.Api/AgriAssist.Api.csproj --startup-project backend/AgriAssist.Api/AgriAssist.Api.csproj --output-dir Migrations
```

Review migration/designer/snapshot together. Add a `PostgreSqlCollection` test proving two linked Routine rows are allowed and a second linked PrePlanting row is rejected.

**Step 6: Verify and commit**

```powershell
dotnet build backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~ValidatorTests|FullyQualifiedName~CropPlanningAiWorkflowTests'
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~PrePlantingAssessmentPostgreSqlIntegrationTests
git status --short
git diff --check
git diff -- backend/AgriAssist.Api backend/AgriAssist.Api.Tests
git add backend/AgriAssist.Api backend/AgriAssist.Api.Tests
git commit -m 'feat: enforce linked pre-planting assessments'
git rev-parse --short HEAD
git status --short
```

Record a PostgreSQL skip without claiming database verification when the connection variable is absent.

### Task 2: Restrict raw PrePlanting evidence and mutation paths

**Files:**
- Modify: `backend/AgriAssist.Api/Services/Inspections/InspectionService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/Inspections/InspectionsController.cs`
- Modify: `backend/AgriAssist.Api/Controllers/CropPlanning/CropPlansWorkflowController.cs`
- Modify: `backend/AgriAssist.Api.Tests/InspectionWorkflowTests.cs`
- Create: `backend/AgriAssist.Api.Tests/PrePlantingAuthorizationIntegrationTests.cs`

**Step 1: Write failing authorization tests**

Prove Farmer and Resource Officer cannot list/get raw PrePlanting inspections, observations, issues, images, history, or follow-ups; Agricultural Officer/Admin are read-only; only the owning Field Officer may mutate a draft; generic submission is blocked; completed assessments reject update/observation/image/close; and Routine behavior is unchanged.

Run:

```powershell
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --filter 'FullyQualifiedName~InspectionWorkflowTests|FullyQualifiedName~PrePlantingAuthorizationIntegrationTests' --no-restore
```

Expected: FAIL on the new raw-access and immutability cases.

**Step 2: Centralize visibility and mutation guards**

Apply one visibility rule across inspections and dependent queries: preserve Routine access; expose PrePlanting raw evidence only to Field Officer/Agricultural Officer/Admin. Use safe not-found responses for direct unauthorized lookups.

For PrePlanting mutation require Field Officer, exact `InspectorUserId`, draft state, and the dedicated crop-plan submission route. Keep Routine behavior unchanged.

Restrict raw assessment/context/full-result endpoints to Field Officer/Agricultural Officer/Admin and write/run routes to Field Officer, with service ownership checks authoritative.

**Step 3: Verify and commit**

```powershell
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --filter 'FullyQualifiedName~InspectionWorkflowTests|FullyQualifiedName~PrePlantingAuthorizationIntegrationTests|FullyQualifiedName~CropPlanningAiWorkflowTests' --no-restore
git status --short
git diff --check
git diff -- backend/AgriAssist.Api backend/AgriAssist.Api.Tests
git add backend/AgriAssist.Api backend/AgriAssist.Api.Tests
git commit -m 'fix: restrict pre-planting evidence access'
git rev-parse --short HEAD
git status --short
```

### Task 3: Build the structured React Field Officer experience

**Files:**
- Modify: `frontend/react-app/src/types.ts`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.css`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`
- Modify if needed: `frontend/react-app/src/pages/WorkflowReviewPage.tsx`
- Modify if needed: `frontend/react-app/src/App.test.tsx`

**Step 1: Write failing UI tests**

Cover context display, incomplete draft save, untouched risks sending null, explicit no-risk sending an empty list, evidence upload after inspection creation, separate Save/Submit/Run calls, disabled pre-submit Run, immutable submission, running state, failed Retry, completed read-only output, Agricultural Officer/Admin read-only controls, and no Resource Officer/Farmer panel route.

Run:

```powershell
Set-Location frontend/react-app
npm test -- src/pages/PrePlantingAssessmentPanel.test.tsx
```

Expected: FAIL because structured controls and separate lifecycle do not exist.

**Step 2: Extend TypeScript contracts and context loading**

Add exact string unions, nullable assessment fields, `identifiedRisks: PrePlantingRisk[] | null`, and `PrePlantingContext`. Load context and assessment for workflows containing a FieldAnalysis step. Show completed historical data read-only, but do not show the panel for unrelated workflows.

**Step 3: Implement structured controls**

Use accessible selects and bounded notes. Model risks explicitly:

```typescript
type RiskAssessmentState = 'unassessed' | 'none' | 'selected'

function risksForRequest(
  state: RiskAssessmentState,
  selected: PrePlantingRisk[],
): PrePlantingRisk[] | null {
  if (state === 'unassessed') return null
  if (state === 'none') return []
  return selected
}
```

Loading null preserves unassessed. Selecting None produces an empty list. Selected checkboxes produce a duplicate-free list.

**Step 4: Separate actions and render all states**

- Save Draft performs PUT only and allows incompleteness.
- Upload Evidence requires a saved inspection ID.
- Submit saves current values, uploads pending files, then calls dedicated submit; it does not run AI.
- Run AI appears only after submission.
- Retry appears only for retryable Member 2 failure.

Render structured results plus warnings/human-review state. Keep raw notes inside the authorized assessment view.

**Step 5: Verify and commit**

```powershell
Set-Location frontend/react-app
npm test -- src/pages/PrePlantingAssessmentPanel.test.tsx
npm run lint
npm run build
Set-Location ../..
git status --short
git diff --check
git diff -- frontend/react-app
git add frontend/react-app
git commit -m 'feat: add structured pre-planting field assessment form'
git rev-parse --short HEAD
git status --short
```

### Task 4: Require exact evidence and retryable/idempotent AI execution

**Files:**
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/Internal/InternalAgentToolsController.cs`
- Modify: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`
- Modify: `ai-service/tools/inspection_tools.py`
- Modify: `ai-service/agents/crop_field_analysis_agent.py`
- Modify: `ai-service/tests/test_crop_field_analysis_agent.py`

**Step 1: Write failing backend and AI tests**

Backend cases: missing/draft evidence leaves state unchanged; persisted structured observations are revalidated; mismatched IDs/purpose/state/owner reject; SafeFailure remains non-terminal at Member 2; retry reuses evidence; successful retry advances once; completed run is idempotent; Running conflicts; concurrent acquisition calls AI once.

AI cases: valid pre-planting examples replace crop-health examples; missing inspection is SafeFailure; crop-plan context is loaded; missing required observation/marker fails safely; reference-data unavailability fails safely; exact IDs reach every evidence tool; images stay metadata-only; notes cannot alter instructions.

Run:

```powershell
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --filter FullyQualifiedName~CropPlanningAiWorkflowTests --no-restore
Set-Location ai-service
python -m pytest tests/test_crop_field_analysis_agent.py -q
```

Expected: FAIL on new context, SafeFailure, retry, and concurrency assertions.

**Step 2: Add scoped crop-plan context and evidence validation**

Add `InspectionTools.get_crop_plan_context` using the existing scoped internal endpoint. Include crop plan, field/cycle, exact normalized inspection, exact issues, metadata-only images, and verified reference data in evidence.

Return SafeFailure when inspection is absent/mismatched/unsubmitted, required observations or risk marker are missing, or verified reference data is unavailable.

**Step 3: Revalidate and acquire backend runs**

Before AI, reload and validate exact request/field/inspection/purpose/completed state, owning Field Officer, current Member 2 step, and persisted submission rules. Completed returns stored output; Running conflicts; only Pending/retryable Failed may acquire.

Set Running, clear retry errors, increment `AgentWorkflow.Version`, and save before the external call. Convert concurrency exceptions to a safe conflict. After AI, recheck acquisition and atomically persist validation/output/step/workflow/version.

SafeFailure/provider failure sets the step Failed/retryable, workflow Pending, current step Member 2, and workflow completion null. Success alone completes the step and advances to WeatherResourceAgent.

**Step 4: Verify and commit**

```powershell
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --filter FullyQualifiedName~CropPlanningAiWorkflowTests --no-restore
Set-Location ai-service
python -m pytest tests/test_crop_field_analysis_agent.py -q
Set-Location ..
git status --short
git diff --check
git diff -- backend/AgriAssist.Api backend/AgriAssist.Api.Tests ai-service
git add backend/AgriAssist.Api backend/AgriAssist.Api.Tests ai-service
git commit -m 'fix: require exact pre-planting evidence for field analysis'
git rev-parse --short HEAD
git status --short
```

### Task 5: Add structured output and observation-first deterministic analysis

**Files:**
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/CropPlanningWorkflowDtos.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`
- Modify: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`
- Modify: `ai-service/schemas/field_analysis.py`
- Modify: `ai-service/agents/crop_field_analysis_agent.py`
- Modify: `ai-service/tests/test_crop_field_analysis_agent.py`
- Modify: `frontend/react-app/src/types.ts`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`
- Modify: `docs/ai-usage/member-2-crop-planning-contract.md`

**Step 1: Write failing structured-output and priority tests**

Use table-driven tests for every approved High/Medium/Low rule, empty issues with decisive observations, issues raising but never lowering priority, invalid evidence producing SafeFailure/Unknown, actual observation values appearing in deterministic sections, backend validation/serialization, and React rendering. Retain assertions for legacy `fieldCondition`, `openIssues`, and `priority`.

Run:

```powershell
Set-Location ai-service
python -m pytest tests/test_crop_field_analysis_agent.py -q
Set-Location ../backend/AgriAssist.Api.Tests
dotnet test AgriAssist.Api.Tests.csproj --filter FullyQualifiedName~CropPlanningAiWorkflowTests --no-restore
Set-Location ../../frontend/react-app
npm test -- src/pages/PrePlantingAssessmentPanel.test.tsx
```

Expected: FAIL because structured output and observation-first priority are absent.

**Step 2: Extend contracts backward-compatibly**

Add field suitability, soil/water/drainage assessments, preparation requirements, planting readiness, identified risks, and recommended actions. SafeFailure emits empty sections/lists plus Unknown values. Validate non-null arrays, bounded text, allowed suitability/readiness/priority/risk values, and evidence IDs.

**Step 3: Implement deterministic analysis**

Normalize exact observations, calculate base priority from current assessment first, calculate exact-issue priority separately, and take the higher rank. Build summaries, requirements, risks, and actions only from recorded values. Do not infer images, crop disease, chemicals, schedules, approval, weather, or inventory.

**Step 4: Strengthen provider prompt and consumers**

Require the extended JSON shape and validate unknown IDs/codes/unsafe language. Extend backend deserialization and React display without removing legacy fields. Replace all Member 2 leaf-yellowing/post-planting examples with soil, water, drainage, access, preparation, or readiness examples. Update the persisted Member 2 contract.

**Step 5: Verify and commit**

```powershell
Set-Location ai-service
python -m pytest tests/test_crop_field_analysis_agent.py -q
Set-Location ../backend/AgriAssist.Api.Tests
dotnet test AgriAssist.Api.Tests.csproj --filter FullyQualifiedName~CropPlanningAiWorkflowTests --no-restore
Set-Location ../../frontend/react-app
npm test -- src/pages/PrePlantingAssessmentPanel.test.tsx
Set-Location ../..
git status --short
git diff --check
git diff -- backend ai-service frontend/react-app docs/ai-usage
git add backend ai-service frontend/react-app docs/ai-usage
git commit -m 'feat: enrich pre-planting field analysis output'
git rev-parse --short HEAD
git status --short
```

### Task 6: Expose only a safe completed Member 2 summary to Member 3

**Files:**
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/CropPlanningWorkflowDtos.cs`
- Modify: `backend/AgriAssist.Api/Dtos/Resources/WeatherResourceDtos.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/ICropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/CropPlanning/CropPlansWorkflowController.cs`
- Modify: `backend/AgriAssist.Api/Services/Resources/WeatherResourceWorkflowService.cs`
- Modify: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`
- Modify: `backend/AgriAssist.Api.Tests/WeatherResourceWorkflowTests.cs`
- Modify: `backend/AgriAssist.Api.Tests/PrePlantingAuthorizationIntegrationTests.cs`
- Modify: `ai-service/schemas/weather_resource.py`
- Modify: `docs/ai-usage/member-2-crop-planning-contract.md`
- Modify: `docs/ai-usage/member-3-weather-resource-contract.md`

**Step 1: Write failing boundary tests**

Prove Resource Officer cannot read or mutate raw Member 2 data/full result, can read the safe summary only for an exact workflow whose field-analysis step is Completed and current step is WeatherResourceAgent, cannot read a premature/unrelated plan, and passes no raw notes/observations/images/evidence IDs/issue IDs. Prove the captured `WeatherResourceInput` contains water, drainage, readiness, risk, and preparation context without manual re-entry.

Run:

```powershell
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --filter 'FullyQualifiedName~WeatherResourceWorkflowTests|FullyQualifiedName~PrePlantingAuthorizationIntegrationTests|FullyQualifiedName~CropPlanningAiWorkflowTests' --no-restore
```

Expected: FAIL because the safe handoff and structured Member 3 context do not exist.

**Step 2: Create the safe handoff**

Expose only workflow/request/field/cycle identity, location/dates, suitability, soil/water/drainage summaries, preparation requirements, readiness, risk codes, recommended actions, legacy field summary/priority, warnings, and human-review state. Remove evidence inspection IDs and raw issue summaries from the Resource Officer-visible boundary.

For Resource Officer require the exact latest request workflow, Completed field-analysis step, current WeatherResourceAgent step, and valid Analyzed output. Deny Farmer. Keep full staff result limited to Field Officer/Agricultural Officer/Admin.

**Step 3: Pass safe context into Member 3**

Add an optional read-only `Member2FieldAnalysisContext` to `WeatherResourceInput` while preserving legacy priority/summary fields. Populate it from persisted output and mirror it in the Python schema. Do not alter Member 3 weather/inventory reasoning.

**Step 4: Verify and commit**

```powershell
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --filter 'FullyQualifiedName~WeatherResourceWorkflowTests|FullyQualifiedName~PrePlantingAuthorizationIntegrationTests|FullyQualifiedName~CropPlanningAiWorkflowTests' --no-restore
Set-Location ai-service
python -m compileall -q .
python -m pytest tests/test_weather_resource_agent.py -q
Set-Location ..
git status --short
git diff --check
git diff -- backend ai-service docs/ai-usage
git add backend ai-service docs/ai-usage
git commit -m 'test: verify weather resource handoff'
git rev-parse --short HEAD
git status --short
```

### Task 7: Run the full integration verification gate

**Files:**
- Modify only files needed to repair failures caused by Tasks 1-6.

**Step 1: Confirm repository safety before the full gate**

```powershell
git branch --show-current
git status --short
rg -n '^(<<<<<<<|=======|>>>>>>>)' --glob '!node_modules/**' --glob '!bin/**' --glob '!obj/**'
git diff --check
```

Expected: branch is `Field-Inspection-&-Crop-Issue-Management`, no conflict markers, and no whitespace errors.

**Step 2: Run the backend gate**

```powershell
dotnet restore backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj
dotnet build backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-restore
$env:Logging__EventLog__LogLevel__Default = 'None'
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-build
```

Run the PostgreSQL collection with `AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING` configured. If it is unavailable, report those provider-specific tests as skipped rather than claiming the partial-index behavior was verified.

**Step 3: Run the AI-service gate**

```powershell
Set-Location ai-service
python -m pip install -r requirements.txt
python -m compileall -q .
python -m pytest
Set-Location ..
```

**Step 4: Run the React gate**

```powershell
Set-Location frontend/react-app
npm ci
npm run lint
npm test
npm run build
Set-Location ../..
```

No Flutter files are planned. Run Flutter checks only if implementation changes a shared contract consumed by the mobile application.

**Step 5: Repair only introduced failures and commit the repair**

If Tasks 1-6 caused integration failures, make the smallest scoped repair, rerun the affected focused test, then repeat the complete gate. Commit only after all applicable checks pass:

```powershell
git status --short
git diff --check
git diff
git add backend ai-service frontend/react-app docs
git commit -m 'fix: resolve pre-planting field analysis integration issues'
```

Skip this commit when no repair is needed.

**Step 6: Produce the final evidence report**

```powershell
git branch --show-current
git log --oneline --decorate -10
git status --short
git diff --check
```

Report each command and result, any PostgreSQL/Flutter check that could not run, migration/provider limitations, the local commit hashes, and confirmation that no push or pull request was created.

## Execution handoff

Execute Tasks 1-7 sequentially in this repository. Stop after a failing verification that cannot be safely repaired within the approved design. Never combine incomplete work into a commit, never switch branches, and never push.
