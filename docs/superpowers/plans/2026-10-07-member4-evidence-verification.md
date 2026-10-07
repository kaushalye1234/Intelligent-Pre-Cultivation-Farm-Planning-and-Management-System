# Member 4 Evidence Verification Implementation Plan

> **For agentic workers:** Execute this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Use the approved spec and AGENTS.md; no delegated agents are required.

**Goal:** Let an Agricultural Officer verify a complete crop reference and let an Admin start a replacement workflow on the same plan, with Member 3 and Member 4 pinned to that verified profile.

**Architecture:** New reference versions begin inactive and receive server-recorded verification after an officer reviews field water regime, sources, stages, and rules. A guarded replacement run keeps the old blocked workflow intact, pins the verified profile, then uses the existing role-restricted agents, approval transaction, guide generation, and farmer response.

**Tech Stack:** ASP.NET Core 8, EF Core 8/PostgreSQL, FastAPI/LangGraph, React/TypeScript/Vitest, Flutter.

**Spec:** `docs/superpowers/specs/2026-10-07-member4-evidence-verification-design.md`

## Global Constraints

- The same Agricultural Officer may prepare and verify, but verification is a separate action with server-recorded actor and UTC time.
- Admin starts the replacement workflow; Field Officer, Resource Officer, and Agricultural Officer retain their existing handoffs.
- Preserve the old blocked workflow and candidate; never create final tasks, irrigation, or reservations before explicit approval.
- The replacement uses the existing request's planning window only while it is future-dated. A stale window needs a separately authorized new request.
- Historical reference rows become `LegacyReviewRequired`, never silently `Verified`; unrelated legacy flows stay available during this rollout.
- Do not create or activate a live Bg 352 profile before an Agricultural Officer verifies the field regime and source values.

## Review Focus

- A draft with `VerifiedAt` supplied by an old client must remain unverified; test in Task 1.
- An officer verifies a draft while another editor changes a rule; test optimistic conflict in Task 1.
- A replacement run's pinned profile is deactivated before Member 3; test safe `MissingDependency` in Task 2.
- Two Admins start replacement concurrently with different keys; test exactly one new workflow in Task 3.
- A guide is unavailable after approval; test approved work remains visible and officer retry is available in Task 4.

---

### Task 1: Verified reference versions and field regime

**Files:**
- Modify: `backend/AgriAssist.Api/Models/CropPlanning/CropReferenceProfile.cs`, `backend/AgriAssist.Api/Data/AppDbContext.cs`, `backend/AgriAssist.Api/Dtos/CropPlanning/CropPlanningDtos.cs`, `backend/AgriAssist.Api/Validators/CropPlanning/CropPlanningValidators.cs`, `backend/AgriAssist.Api/Controllers/CropPlanning/CropPlanningController.cs`, `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`
- Create: `backend/AgriAssist.Api/Models/CropPlanning/FieldWaterRegimeVerification.cs`, `backend/AgriAssist.Api/Services/CropPlanning/CropReferenceVerificationService.cs`, matching EF migration and snapshot update
- Test: `backend/AgriAssist.Api.Tests/CropReferenceVerificationTests.cs`, `backend/AgriAssist.Api.Tests/CropReferenceProfileAuthorizationIntegrationTests.cs`

**Interfaces:**
- `CropReferenceProfile.VerificationState: CropReferenceVerificationState`, `VerifiedByUserId: Guid?`, `VerifiedAt: DateTime?`, `DraftVersion: int`, `WaterRegime: WaterRegime?`, `FieldWaterRegimeVerificationId: Guid?`.
- `FieldWaterRegimeVerification`: field ID, regime (`Irrigated`/`Rainfed`), observation, verified officer ID and UTC time.
- `VerifyReferenceRequest(Guid FieldId, WaterRegime WaterRegime, string Observation, int ExpectedDraftVersion, bool Confirmed)`; `POST /api/crop-planning/crop-reference-profiles/{id}/verify` returns updated details.
- Existing profile POST creates an inactive Draft and ignores no claimed verification: a supplied `VerifiedAt` is rejected. Draft edit uses `PUT /crop-reference-profiles/{id}/draft` with expected version. Existing `/active` rejects activation unless Verified, except it may deactivate any active legacy row.

- [ ] Write failing tests: draft has null verification actor/time and inactive state; supplied `VerifiedAt` cannot verify; same officer can verify; wrong role cannot; missing stage/rule/source/regime/resource match fails; concurrent edit/verify conflicts; legacy migration preserves data without asserting review.
- [ ] Run targeted xUnit tests and confirm the new assertions fail.
- [ ] Implement models, mapping, migration, DTOs, validation and service/controller actions. Verified content is immutable; edits create a new draft version. Expose stage/rule source URLs in detail response.
- [ ] Run targeted xUnit and isolated PostgreSQL migration/concurrency tests; expect pass. Run `git diff --check` and commit Task 1.

### Task 2: Pin Member 3 and Member 4 to the verified source

**Files:**
- Modify: `backend/AgriAssist.Api/Models/Shared/AgentWorkflow.cs`, `backend/AgriAssist.Api/Data/AppDbContext.cs`, `backend/AgriAssist.Api/Services/Resources/CropResourceRequirementService.cs`, `backend/AgriAssist.Api/Controllers/Internal/InternalAgentToolsController.cs`, `backend/AgriAssist.Api/Services/TaskApproval/SchedulingEvidenceBuilder.cs`, migration/snapshot from Task 1
- Test: `backend/AgriAssist.Api.Tests/CropResourceRequirementTests.cs`, `backend/AgriAssist.Api.Tests/SchedulingEvidenceBuilderTests.cs`, `backend/AgriAssist.Api.Tests/WeatherResourceWorkflowTests.cs`

**Interfaces:**
- `AgentWorkflow.RequiredCropReferenceProfileId: Guid?` applies only to replacement runs.
- `ICropResourceRequirementService.GetRequirementsAsync(Guid cropPlanRequestId, Guid? requiredProfileId, CancellationToken)` returns `Unavailable` when a required profile is missing, inactive, unverified, wrong crop/variety/region, or wrong field water regime; null preserves legacy selection.
- Internal Member 3 tool resolves the workflow's required ID server-side, never from a caller-provided query string. Member 4 evidence builder selects that same ID and rejects differing `requirementSource.cropReferenceProfileId`.

- [ ] Write failing tests for matching pin, mismatched pin, deactivation, changed verification version, and null-pin legacy behavior.
- [ ] Run targeted tests to confirm failure.
- [ ] Implement server-side pin resolution and selection in Member 3 and Member 4; keep existing JSON contracts unless a typed field is needed by the AI service.
- [ ] Run targeted backend and Python scheduler tests; expect pass. Commit Task 2.

### Task 3: Guarded replacement workflow

**Files:**
- Modify: `backend/AgriAssist.Api/Models/Shared/AgentWorkflow.cs`, `backend/AgriAssist.Api/Data/AppDbContext.cs`, `backend/AgriAssist.Api/Dtos/CropPlanning/CropPlanningWorkflowDtos.cs`, `backend/AgriAssist.Api/Controllers/CropPlanning/CropPlansWorkflowController.cs`, `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`, corresponding service interface, `backend/AgriAssist.Api/Controllers/TaskApproval/TaskApprovalController.cs`, `backend/AgriAssist.Api/Services/TaskApproval/WorkflowApprovalService.cs`, migration/snapshot
- Test: `backend/AgriAssist.Api.Tests/CropPlanningWorkflowAuthorizationIntegrationTests.cs`, `backend/AgriAssist.Api.Tests/WorkflowApprovalTests.cs`, new PostgreSQL replacement integration test

**Interfaces:**
- `AgentWorkflow.SupersedesWorkflowId: Guid?`, `ReplacementIdempotencyKey: Guid?`, `RequiredCropReferenceProfileId: Guid?`.
- `StartReplacementRequest(Guid BlockedWorkflowId, Guid VerifiedProfileId, Guid IdempotencyKey)`; Admin-only `POST /api/crop-plans/{id}/replace-blocked-workflow` returns the new workflow ID and current step.
- `IWorkflowApprovalService.GetEvidenceResolutionAsync(Guid workflowId, CancellationToken)` powers AO/Admin `GET /api/task-approval/workflows/{id}/evidence-resolution`: crop/variety/field, pinned ID, persisted blocking reasons, profile/source details, successor workflow ID, and next responsible role.
- Service starts a new AgentWorkflow on the existing CropPlanRequest using the existing future planning window. The latest workflow must be `MissingDependency` or `CandidateBlocked`, with no terminal plan state or active replacement. Same key and payload returns the existing replacement; different key conflicts. Old workflow rows remain unchanged.

- [ ] Write failing role, stale window, wrong plan/profile, old-row-preservation, resolution-read, idempotency and concurrent-start PostgreSQL tests.
- [ ] Run them and confirm failure.
- [ ] Extract shared coordinator-start logic from `StartAiWorkflowAsync`; add the guarded replacement entry point, resolution-read DTO, transactional row locking, unique idempotency index, and supersession link.
- [ ] Run targeted backend and PostgreSQL tests; expect pass. Commit Task 3.

### Task 4: Officer resolution page and guide retry

**Files:**
- Create: `frontend/react-app/src/pages/WorkflowEvidenceResolutionPage.tsx`, `frontend/react-app/src/pages/WorkflowEvidenceResolutionPage.test.tsx`
- Modify: `frontend/react-app/src/App.tsx`, `frontend/react-app/src/pages/WorkflowReviewPage.tsx`, `frontend/react-app/src/pages/AdminCropManagement.tsx`, `frontend/react-app/src/types.ts`, relevant CSS and tests

**Interfaces:**
- Route `/task-approval/workflows/:id/resolve-evidence` for Agricultural Officer and Admin. Backend returns blocking evidence, draft/verification state, pinned profile, source details, and next responsible role using typed DTOs; React does not infer authority from button visibility.
- `Verify and activate` is AO-only; `Start replacement workflow` is Admin-only; existing approval and guide retry endpoint are AO/Admin-only. Each action refreshes the persisted workflow/progress response.

- [ ] Write failing Vitest cases for blocked details, draft errors, same-officer verification, Admin handoff, replacement link, wrong-role controls, unavailable guide and retry feedback.
- [ ] Run targeted Vitest to confirm failure.
- [ ] Add the page, route, typed API calls and status/role views; preserve old workflow as read-only history and link its replacement.
- [ ] Run targeted Vitest, React lint/build, then browser-check desktop and mobile viewport against disposable local data. Commit Task 4.

### Task 5: Integrated proof and rollout preparation

**Files:**
- Modify: `docs/plans/member4-approval-recovery-evidence.md` with actual results and still-unverified live facts; update `scripts/member4-approval-demo` only if needed for disposable fixtures.

**Interfaces:**
- Local proof uses a disposable PostgreSQL database and synthetic users. Live proof requires the officer's genuine field/source decision, reviewed PR merge, migration backup, and exact deployed commit checks.

- [ ] Run backend restore/build/test, isolated PostgreSQL migration/rollback/transaction tests, AI compile/pytest, React lint/build/test, Flutter pub get/analyze/test.
- [ ] Execute a new local synthetic workflow through Member 1–4 using a verified synthetic profile. Assert Member 3 and Member 4 profile IDs match, old run unchanged, approval creates exactly one final set, guide Ready/Unavailable/retry, and farmer ownership.
- [ ] Record commands/results and deployment prerequisites in the evidence document; run conflict-marker scan and `git diff --check`; commit.
- [ ] Open a reviewed PR stacked on #87. Do not merge or activate a real profile without the corresponding human review and officer decision.
