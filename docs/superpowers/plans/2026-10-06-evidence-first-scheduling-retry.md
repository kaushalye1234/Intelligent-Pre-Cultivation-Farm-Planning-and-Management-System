# Evidence-First Scheduling Retry Implementation Plan

> **For agentic workers:** Execute this plan natively, task by task. Steps use checkbox syntax for tracking.

**Goal:** Let an Agricultural Officer correct missing evidence or a too-short date window, refresh the dependent analysis, and safely retry scheduling.

**Architecture:** Reuse the existing AI resource-research endpoint and its source-review UI. Add an audited workflow retry operation that can revise only the date window, re-run Member 3, and then regenerate the Member 4 candidate. Keep AI results unverified until an Admin confirms the cited recommendation, and retain all current candidate and approval validators.

**Tech Stack:** ASP.NET Core 8, EF Core 8, PostgreSQL, FastAPI/LangGraph resource research, React/TypeScript/Vite, xUnit, Vitest.

**Spec:** `docs/superpowers/specs/2026-10-06-evidence-first-scheduling-retry-design.md`

## Global Constraints

- Preserve roles Farmer=1, FieldOfficer=2, ResourceOfficer=3, AgriculturalOfficer=4, Admin=5.
- No AI-generated value becomes verified without explicit Admin confirmation; Agricultural Officers may research and review drafts but cannot verify/save them.
- No final task, irrigation schedule, or resource reservation before explicit human approval.
- Save plan-window changes with actor, timestamp, and reason in existing history.
- Keep backend DTOs and frontend contracts aligned; use UTC timestamps.
- Preserve workflow versioning, idempotency, inventory transactions, and concurrency checks.
- Do not add migrations unless the existing audit model cannot represent the change.

## Review Focus

- Stale workflow version or concurrent retry returns a conflict and creates no duplicate candidate.
- Resource research returns no recommendation or conflicting sources and saves nothing.
- Officer changes dates but retries still fail due to verified stage duration; the UI explains the remaining issue.
- Retry is attempted on an approved, rejected, cancelled, or currently running workflow.
- A refreshed Member 3 response is demonstrably the input used by Member 4.

---

### Task 1: Permit Agricultural Officer research while keeping verification Admin-only

**Files:**
- Modify: `backend/AgriAssist.Api/Controllers/Resources/ResourceRequirementResearchController.cs`
- Modify: `backend/AgriAssist.Api/Services/Resources/ResourceRequirementResearchService.cs`
- Modify: `backend/AgriAssist.Api/Dtos/Resources/ResourceRequirementResearchDtos.cs` only if the actor identifier needs a contract-neutral rename.
- Modify: `frontend/react-app/src/components/ResourceRequirementResearchPanel.tsx`
- Test: `backend/AgriAssist.Api.Tests/ResourceRequirementResearchTests.cs`
- Test: `frontend/react-app/src/components/ResourceRequirementResearchPanel.test.tsx`

- [x] Add failing tests proving AgriculturalOfficer may research but cannot verify/save, while unrelated roles remain forbidden; Admin can verify/save and research alone writes no rule.
- [x] Run the focused tests and confirm the expected failures.
- [x] Add AgriculturalOfficer authorization only for draft research; keep verification Admin-only and update UI to clearly identify this boundary.
- [x] Run focused backend and React tests.

### Task 2: Add audited date-window correction and downstream retry

**Files:**
- Modify: `backend/AgriAssist.Api/Controllers/TaskApproval/TaskApprovalController.cs`
- Modify: `backend/AgriAssist.Api/Services/TaskApproval/WorkflowApprovalService.cs`
- Modify: `backend/AgriAssist.Api/Services/Resources/WeatherResourceWorkflowService.cs`
- Modify: `backend/AgriAssist.Api/Dtos/TaskApproval/WorkflowApprovalDtos.cs`
- Test: `backend/AgriAssist.Api.Tests/WorkflowApprovalTests.cs`
- Test: `backend/AgriAssist.Api.Tests/WeatherResourceWorkflowTests.cs`

- [x] Add failing tests for eligible blocked retry, date-history audit, new Member 3 output flowing into Member 4, and rejected stale/terminal/running requests.
- [x] Run those tests and verify failure is due to missing retry behavior.
- [x] Implement a version-checked retry request; validate status/role/date range/reason and update `CropPlanRequestHistory` when dates change.
- [x] Implement a controlled Member 3 refresh for a blocked workflow, then call candidate generation; stop safely if the refresh fails.
- [x] Advance the revision for blocked/failed retries; reuse the revision already opened by a revision-request decision; append validation results without creating final work.
- [x] Run focused backend tests, then the full backend test suite.

### Task 3: Add missing-evidence assistance and retry controls to workflow review

**Files:**
- Modify: `frontend/react-app/src/pages/WorkflowReviewPage.tsx`
- Modify: `frontend/react-app/src/pages/WorkflowReviewPage.test.tsx`
- Modify: `frontend/react-app/src/components/ResourceRequirementResearchPanel.tsx`
- Modify: `frontend/react-app/src/resourceRequirementResearch.ts`
- Modify: `frontend/react-app/src/types.ts`

- [x] Add failing UI tests for the retry button, optional date correction/reason, the AI research action, and updated result display.
- [x] Run the focused UI tests and confirm failure.
- [x] Show the current date window and allow the officer to edit it before retry; persist the reason and history.
- [x] Show the existing AI research panel for authorized officers with workflow crop/variety/region preselected; preserve source citations and warnings, but show no verify/save control to non-Admins.
- [x] Add Retry Scheduling for eligible statuses, send the expected version (the backend uses it for concurrency control), then replace review state with the returned review.
- [x] Explain remaining block reasons and keep approval controls hidden unless the candidate is ready.
- [x] Run targeted Vitest tests, lint, and production build.

### Task 4: Verify integrated behavior and prepare the pull request

**Files:**
- Review all files changed by Tasks 1–3.

- [x] Run backend, AI service, React, and Flutter checks required by repository guidance. Backend PostgreSQL-specific integration tests were skipped locally because no isolated test connection string was configured; CI supplies an isolated PostgreSQL service.
- [x] Run `git diff --check` and a conflict-marker scan.
- [ ] Confirm no `.env`, secrets, generated output, or unrelated files are staged.
- [x] Review the final diff and create a focused commit/PR to `dev` with test evidence and the remaining data prerequisites explained (PR #81).
