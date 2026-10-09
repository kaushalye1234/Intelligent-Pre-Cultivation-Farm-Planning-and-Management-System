# Final Cultivation Guide Implementation Plan

> **For agentic workers:** Implement task-by-task and review each task before continuing. Keep changes local; do not change branches, push, rebase, force-push, or commit unless separately requested.

**Goal:** Add a farmer-facing, structured cultivation guide generated from the currently approved Member 1–4 workflow, without changing any approved values or decisions.

**Architecture:** ASP.NET Core remains the source of truth and gathers persisted, revision-matched evidence. After an approval transaction commits, it requests a structured guide from the existing FastAPI/OpenAI boundary, validates every authoritative value deterministically, and stores the guide against the approved workflow revision. The existing Flutter approved-plan screen reads that enriched contract and falls back to the existing approved details whenever guide generation is unavailable.

**Tech Stack:** ASP.NET Core 8, EF Core 8, PostgreSQL, FastAPI, Pydantic, LangGraph, existing OpenAI provider, Flutter/Dart.

**Spec:** User-provided `C:\Users\User\Downloads\AgriAssist_Final_Cultivation_Guide_Agent_Codex_Prompt.md`; this plan maps it to the repository paths inspected on 2026-10-05.

## Implementation Status (2026-10-06)

- [x] Added the authenticated FastAPI guide route, typed schema, provider-backed agent, and focused tests.
- [x] Added ASP.NET orchestration, deterministic validation, post-commit generation, idempotent retry, and safe failure persistence using revision-keyed `AgentStep` records.
- [x] Added the filtered PostgreSQL uniqueness migration and verified its generated SQL offline.
- [x] Extended the existing Flutter approved-plan screen with current-revision guide parsing, approved activities, monthly guidance, and legacy/unavailable fallback.
- [x] Documented the final contract and verified the full AI, backend, React, and Flutter test/build paths applicable to the change.
- [x] Applied and exercised the migration against the disposable PostgreSQL 16 container, including a concurrent two-writer uniqueness test and the complete backend suite with no skipped PostgreSQL tests.
- [x] Completed an isolated HTTP demonstration: officer approval created the approved task and reservation, the configured AI service generated the guide, and the owning farmer read back the current-revision guide through the API.
- [ ] Complete a Flutter Web build and visible screen demonstration; Flutter tests and targeted Dart analysis passed earlier, but analyzer/build attempts stalled without diagnostics and were interrupted.

## Global Constraints

- Run the guide only for a completed Member 4 approval and its exact approved revision.
- Preserve Member 1–4 ownership and all persisted approved dates, quantities, identities, reservations, workflow states, and decisions.
- Flutter calls ASP.NET Core only; OpenAI credentials and AI-service credentials remain server-side.
- Keep the approval transaction independent of provider latency and failure.
- Use current `AI_PROVIDER` / `AI_MODEL` configuration and existing source-policy/provider conventions; do not add a second model or key configuration system.
- Reuse existing approved-plan behavior and JSON/API naming conventions; retain backwards-compatible fields.
- Do not alter unrelated working-tree changes or perform Git operations prohibited by the source prompt.

## Review Focus

- An old guide exists while a newer workflow revision is approved: serve only the current revision; test revision mismatch and stale guide rejection.
- AI output omits, duplicates, or changes an approved task, date, schedule, reservation, or quantity: deterministic validation must reject it; test each authoritative identity/value class.
- Evidence is missing, malformed, or belongs to another workflow: fail safely with no fabricated recommendation; test missing and cross-workflow evidence.
- AI provider times out or returns invalid structured output after approval: retain approval and existing approved-plan data; test provider failure, malformed output, and retry.
- Farmer requests another farmer’s workflow or guide: enforce existing ownership authorization; test owner and non-owner access.

---

## Repository Findings and Decisions

- The approval decision and final `FarmTask`, `IrrigationSchedule`, and reservation writes occur in `WorkflowApprovalService.DecideAsync`; approval is committed before the service returns. Guide generation must occur after that commit and must not be placed inside `BeginTransactionAsync`.
- Flutter currently opens `ApprovedCropPlanScreen` and requests `GET /api/task-approval/workflows/{workflowId}` through `ApiClient.approvedWorkflowDetail`. `ApprovedWorkflowDetail` currently projects field/weather summaries, warnings, recommendations, tasks, and schedules. Extend this existing flow compatibly; do not create a disconnected AI page.
- FastAPI currently wires each workflow endpoint in `ai-service/main.py` to schemas, agents, providers, and graph builders. The configured OpenAI provider/model and `CropFindingTools` source policy are already present and should be reused where their contracts fit.
- The checked-out branch is `member4/explainable-scheduling`, is 30 commits ahead of its remote tracking branch, and has numerous modified and untracked files. Before implementation, preserve and review that existing work; do not reset, clean, switch branches, or stage broad paths.
- No `FinalCultivationGuide` implementation was found in the inspected AI/backend/Flutter paths. Persistence should first be checked against `AgentStep` and existing workflow output storage. If neither can represent an independently retryable, revision-keyed guide without changing completed workflow semantics, add one focused persistence entity and migration.

## File Map

- AI schema and agent: create `ai-service/schemas/final_cultivation_guide.py`, `ai-service/agents/final_cultivation_guide_agent.py`, and focused tests under `ai-service/tests/`; modify `ai-service/main.py` and, only if needed by existing endpoint conventions, `ai-service/graph/workflow_graph.py`.
- Backend contract and orchestration: create typed guide DTOs and a guide AI client/service under `backend/AgriAssist.Api/{Dtos,ExternalServices/AgenticAI,Services}/`; register the service in `Program.cs`; extend `WorkflowApprovalService` only at the post-commit seam; add or extend the farmer approved-plan route and its authorization tests.
- Persistence: if required by the repository findings above, create the guide entity/configuration, update `AppDbContext` and `AppDbContextModelSnapshot`, and add one focused migration. Store `WorkflowId`, `CropPlanRequestId`, approved revision, guide contract version, generation status, generated timestamp, and validated guide JSON with a uniqueness constraint for the workflow/revision.
- Flutter: extend `mobile/flutter_app/lib/models/api_models.dart`, `mobile/flutter_app/lib/services/api_client.dart`, `mobile/flutter_app/lib/state/app_state.dart` only where required, and `mobile/flutter_app/lib/screens/approved_crop_plan_screen.dart`; add tests under `mobile/flutter_app/test/`.
- Documentation: update the AI handoff/API contract docs after implementation choices are verified; do not edit unrelated readiness, report, deployment, or member-contribution files.

## Implementation Tasks

### Task 1: Confirm the existing contracts and select the persistence seam

**Files:**
- Read: `backend/AgriAssist.Api/Services/TaskApproval/WorkflowApprovalService.cs`
- Read: `backend/AgriAssist.Api/Services/TaskApproval/SchedulingEvidenceBuilder.cs`
- Read: `backend/AgriAssist.Api/Controllers/TaskApproval/TaskApprovalController.cs`
- Read: `backend/AgriAssist.Api/Services/TaskApproval/WorkflowApprovalService.cs` response mapping and authorization helpers
- Read: `backend/AgriAssist.Api/Models/Shared/AgentWorkflow.cs` and associated `AgentStep` model/configuration
- Read: `mobile/flutter_app/lib/models/api_models.dart`, `services/api_client.dart`, `screens/approved_crop_plan_screen.dart`
- Read: `ai-service/config.py`, `providers/openai_provider.py`, `tools/crop_finding_tools.py`, and source policy

- [ ] Trace every field from persisted Member 1–4 evidence to the current workflow-detail response and record the exact DTO/model property names used by this branch.
- [ ] Confirm farmer ownership checks and approved workflow/revision checks in the current detail route; add these checks to the implementation design if they are not already enforced.
- [ ] Decide whether `AgentStep` can safely store a post-approval guide with pending/failed/retry states while the workflow remains completed. If it cannot, use the focused revision-keyed guide entity described in the File Map; do not mutate workflow status to represent guide status.
- [ ] Record the selected trigger and retry behavior: create/mark guide generation pending after the approval commit, call the AI service outside the transaction, persist only validated output, and preserve approval plus existing farmer-visible approved records on every failure.

**Deliverable:** A verified, branch-specific source-to-screen contract and a persistence decision before product code changes.

### Task 2: Define the structured guide contract and safe AI agent

**Files:**
- Create: `ai-service/schemas/final_cultivation_guide.py`
- Create: `ai-service/agents/final_cultivation_guide_agent.py`
- Modify: `ai-service/main.py`
- Test: `ai-service/tests/test_final_cultivation_guide_agent.py`
- Test: `ai-service/tests/test_final_cultivation_guide_api.py`

**Interfaces:**
- Input: versioned, normalized evidence for one approved workflow/revision; exclude raw database entities, unnecessary personal data, and untrusted free-text instructions.
- Output: versioned structured farmer guide with only evidence-backed narrative, supported risks, source references, and exact approved activity IDs/dates/quantities.
- Route: `POST /workflows/crop-planning/final-cultivation-guide`, authenticated with the existing backend tool/service-token convention.

- [ ] Add schema tests for required sections, optional/unknown field handling, invalid IDs, and malformed dates/quantities.
- [ ] Add agent tests proving approved activity values are copied unchanged and external guidance is explanatory, non-authoritative, and cited only from allowed retrieved sources.
- [ ] Run the two new AI tests and confirm they fail for the missing route/schema behavior.
- [ ] Implement `FinalCultivationGuideAgent` using the existing provider configuration and source-policy/retrieval conventions; do not create a second OpenAI client or duplicate allowlist.
- [ ] Wire the route using existing safe-failure and sanitized logging conventions. Return structured failure/status without leaking prompts, API keys, raw provider responses, or personal data.
- [ ] Run the focused AI tests; expected result is passing tests for valid output, invalid output, missing evidence, source filtering, prompt injection, timeout, and provider failure.

### Task 3: Aggregate approved evidence and validate the AI response deterministically

**Files:**
- Create: `backend/AgriAssist.Api/Dtos/FinalCultivationGuide/FinalCultivationGuideDtos.cs`
- Create: `backend/AgriAssist.Api/ExternalServices/AgenticAI/IFinalCultivationGuideAIClient.cs`
- Modify: `backend/AgriAssist.Api/ExternalServices/AgenticAI/AgenticAIClient.cs`
- Create: `backend/AgriAssist.Api/Services/FinalCultivationGuide/FinalCultivationGuideService.cs`
- Modify: `backend/AgriAssist.Api/Program.cs`
- Test: `backend/AgriAssist.Api.Tests/FinalCultivationGuideServiceTests.cs`

**Interfaces:**
- `IFinalCultivationGuideAIClient.GenerateAsync(FinalCultivationGuideInput input, CancellationToken cancellationToken) -> Task<FinalCultivationGuideOutput>`.
- `FinalCultivationGuideService.GenerateForApprovedWorkflowAsync(Guid workflowId, int approvedRevision, CancellationToken cancellationToken) -> Task<FinalCultivationGuideStatusResponse>`.
- The service loads persisted, same-workflow Member 1–4 outputs and approved records; it never accepts authoritative values from Flutter.

- [ ] Write tests for incomplete upstream evidence, cross-workflow step IDs, pending/rejected workflows, stale revisions, and mismatched AI response IDs; assert no guide is persisted.
- [ ] Write validator tests for unchanged planting/harvest dates, approved task and schedule identities/dates, reservation identities/quantities, duplicate/missing activities, unknown authoritative records, and unsupported high-impact numeric values.
- [ ] Run the focused tests and confirm the aggregation and validator reject unsafe output before implementation.
- [ ] Implement the read-only evidence builder using the current persisted contracts and approved database rows. Do not rerun Member 1–4 or substitute missing outputs.
- [ ] Implement deterministic validation in ASP.NET Core. The validator, not the LLM, controls whether output can be returned as a valid guide.
- [ ] Implement the typed AI client using existing service URL/token and JSON serialization configuration; register it in `Program.cs` without exposing credentials.
- [ ] Run focused backend tests; expected result is every approved value preserved byte-for-value/identity as appropriate and invalid output rejected.

### Task 4: Persist guides by approval revision and trigger safely after commit

**Files:**
- Conditional create/modify: guide persistence entity and EF configuration under `backend/AgriAssist.Api/Models/` and `Data/`
- Modify: `backend/AgriAssist.Api/Data/AppDbContext.cs`
- Modify: `backend/AgriAssist.Api/Migrations/AppDbContextModelSnapshot.cs`
- Conditional create: one migration under `backend/AgriAssist.Api/Migrations/`
- Modify: `backend/AgriAssist.Api/Services/TaskApproval/WorkflowApprovalService.cs`
- Test: `backend/AgriAssist.Api.Tests/FinalCultivationGuidePersistenceTests.cs`
- Test: `backend/AgriAssist.Api.Tests/WorkflowApprovalTests.cs`

- [ ] Add tests for repeated approval/idempotent generation, unique workflow/revision storage, provider failure after approval, concurrent generation requests, and retry of a failed/pending guide.
- [ ] Run the persistence tests against the repository’s approved test provider; use isolated PostgreSQL for uniqueness/transaction/concurrency claims.
- [ ] Add the minimal persistence structure only if Task 1 confirms existing workflow-step storage cannot represent the guide lifecycle safely. Enforce one guide record per workflow/revision and index current-revision lookup.
- [ ] Create/mark generation pending as part of the committed approval path, then invoke the AI request only after commit. Bound provider duration; catch sanitized provider errors and retain the approved decision and records.
- [ ] Persist a guide only after deterministic validation succeeds. Persist a safe generation status/error category on failure and support an idempotent retry without duplicating guide records.
- [ ] Verify the approval service does not await AI while a database transaction is open and that stale/revised workflows cannot publish the old guide as current.

### Task 5: Expose a farmer-authorized, backward-compatible final-plan contract

**Files:**
- Modify: `backend/AgriAssist.Api/Controllers/TaskApproval/TaskApprovalController.cs` or the existing crop-planning route selected by Task 1
- Modify: `backend/AgriAssist.Api/Services/TaskApproval/WorkflowApprovalService.cs` or the selected farmer-query service
- Modify: `backend/AgriAssist.Api/Dtos/TaskApproval/WorkflowApprovalDtos.cs` or create an approved-plan DTO
- Test: `backend/AgriAssist.Api.Tests/FinalCultivationGuideAuthorizationTests.cs`
- Test: `backend/AgriAssist.Api.Tests/FinalCultivationGuideApiTests.cs`

- [ ] Add API tests for farmer owner, other farmer, officer/admin, unapproved workflow, unavailable guide, stale revision, and legacy workflow response without a guide.
- [ ] Extend the existing approved workflow detail contract or add one related approved-plan route only if its current shape cannot evolve compatibly. Preserve all existing `workflow`, `steps`, `decisions`, warnings, recommendations, and approved task/schedule fields consumed by Flutter.
- [ ] Return guide status separately from approved workflow status; return only validated guide output for the current approved revision.
- [ ] Enforce authentication, ownership, and approved status in ASP.NET Core. Never reveal internal prompts, raw AI outputs, tool tokens, or API keys.
- [ ] Run focused API tests; expected result is owner-only access and unchanged legacy response behavior.

### Task 6: Render the enriched guide in the existing Flutter Final Plan

**Files:**
- Modify: `mobile/flutter_app/lib/models/api_models.dart`
- Modify: `mobile/flutter_app/lib/services/api_client.dart`
- Modify: `mobile/flutter_app/lib/screens/approved_crop_plan_screen.dart`
- Test: `mobile/flutter_app/test/approved_crop_plan_screen_test.dart`
- Test: `mobile/flutter_app/test/crop_plan_api_test.dart`

**Interfaces:**
- Parse optional guide status/version/content from the ASP.NET approved-plan response; preserve support for older responses with no guide fields.
- Display the farmer-focused week actions, current stage, monthly guidance, upcoming approved activities, field/weather/resource context, supported warnings, harvest preparation, rationale, and farmer-appropriate sources in the existing screen.

- [ ] Add widget/API tests for a complete guide, legacy response, pending/unavailable guide fallback, empty optional sections, and approved activity date/value preservation.
- [ ] Implement backward-compatible parsing with conservative defaults; do not infer missing authoritative values in Dart.
- [ ] Extend the existing screen and theme components. Keep approved activities visually distinct from non-authoritative explanatory advice, and show a clear temporary-unavailable message while retaining currently available approved tasks and schedules.
- [ ] Run focused Flutter tests; expected result is guide content and legacy fallback both render without changing approval status or approved values.

### Task 7: Regression, security, and integration verification

**Files:**
- Tests: `backend/AgriAssist.Api.Tests/`
- Tests: `ai-service/tests/`
- Tests: `mobile/flutter_app/test/`
- Docs: the confirmed AI usage and API contract docs only

- [ ] Add an end-to-end integration test with fixture outputs for Members 1–4, officer approval, guide generation, current-revision API response, and farmer Flutter parsing. Clearly label fixture/provider verification; do not imply a live OpenAI call unless one is run.
- [ ] Verify rejection and revision paths never generate a guide, and that reapproval produces a distinct current revision without serving stale content.
- [ ] Run affected AI, backend, and Flutter suites using repository commands; run PostgreSQL integration checks for persistence uniqueness/rollback/concurrency if the migration or approval transaction changes.
- [ ] Run `git diff --check`; inspect only task-owned staged/unstaged paths and confirm the pre-existing working-tree changes remain intact.
- [ ] Update contract docs with exact route, schema version, generation statuses, retry behavior, and validation guarantees. Record verification limits accurately.

## Completion Gate

- The guide only uses persisted, matching, approved workflow evidence.
- ASP.NET deterministic validation rejects any mismatch in authoritative identities or values.
- AI failure leaves the approved Member 4 result usable and visible.
- Farmers can read only their own current approved guide through ASP.NET Core.
- The Flutter Final Plan is the single farmer-facing result and supports legacy/unavailable-guide fallback.
- AI keys and raw internal outputs stay server-side.
- Relevant AI, backend, PostgreSQL, Flutter, and regression checks pass with results reported precisely.
