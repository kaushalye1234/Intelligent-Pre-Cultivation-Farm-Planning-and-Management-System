# Crop Plan Cancellation and Archive Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let Farmers and Admins safely cancel any active crop-plan request and let Admins soft-archive only Cancelled or Rejected requests while retaining every workflow and evidence record.

**Architecture:** Extend the existing crop-plan request/history lifecycle rather than adding a parallel cancellation subsystem. A transactional service operation updates request, workflow, active steps, and unfinished pre-planting inspection, while the workflow version and post-AI lease checks prevent late results from reviving cancellation; Admin removal only sets `IsDeleted` after a terminal-state guard.

**Tech Stack:** ASP.NET Core 8, EF Core 8, PostgreSQL, xUnit, React/TypeScript/Vite/Vitest, Flutter/Dart/flutter_test.

---

### Task 1: Define cancellation and audit contracts

**Files:**
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/CropPlanningDtos.cs`
- Modify: `backend/AgriAssist.Api/Models/CropPlanning/CropPlanRequestHistory.cs`
- Modify: `backend/AgriAssist.Api/Data/AppDbContext.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/ICropPlanningService.cs`
- Create: `backend/AgriAssist.Api/Migrations/<timestamp>_AddCropPlanCancellationAudit.cs`
- Create: `backend/AgriAssist.Api/Migrations/<timestamp>_AddCropPlanCancellationAudit.Designer.cs`
- Modify: `backend/AgriAssist.Api/Migrations/AppDbContextModelSnapshot.cs`

1. Add failing model/contract assertions for cancellation request validation and history role/reason/action projection.
2. Run the focused .NET tests and confirm the new members are missing.
3. Add `CropPlanCancellationRequest`, structured history action/role/reason fields, EF length/conversion mappings, service signatures, and an EF migration with nullable columns for compatibility.
4. Run the focused tests and migration model checks.
5. Commit the contract and migration work.

### Task 2: Implement transactional cancellation and soft archive

**Files:**
- Modify: `backend/AgriAssist.Api/Controllers/CropPlanning/CropPlanningController.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`
- Create: `backend/AgriAssist.Api.Tests/CropPlanCancellationTests.cs`
- Modify: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`

1. Write failing tests for Farmer ownership, Admin access and required reason, every allowed active stage, blocked terminal/archived states, workflow version invalidation, step/inspection terminal transitions, preserved evidence, history metadata, archive guards, and replacement request creation.
2. Run the focused tests and verify lifecycle methods/endpoints are absent.
3. Add `POST requests/{id}/cancel` for Farmer/Admin and Admin-only `DELETE requests/{id}`.
4. Implement a transaction that rechecks state, writes history, cancels the request/workflow/unfinished steps/inspection, increments workflow version, and handles concurrency conflicts with structured `409` errors.
5. Implement soft archive for Cancelled/Rejected only, with a same-status archive audit event and no related deletes.
6. Add post-AI request-status and workflow-lease checks wherever a long-running result is persisted so cancellation wins races.
7. Run focused and complete backend tests, then commit.

### Task 3: Add Admin cancellation and archive controls

**Files:**
- Modify: `frontend/react-app/src/pages/CropPlanningPage.tsx`
- Modify: `frontend/react-app/src/pages/CropPlanningPage.test.tsx`
- Modify: `frontend/react-app/src/types.ts`
- Modify: `frontend/react-app/src/styles.css` only if existing page styles cannot express the dialog/action layout

1. Write failing Vitest cases for action visibility, required reason, cancellation POST body, terminal-only delete, confirmation copy, request refresh, and Agricultural Officer exclusion.
2. Run `npm test -- --run src/pages/CropPlanningPage.test.tsx` and confirm failures.
3. Add compact icon actions using the existing modal/dialog system: Admin `Cancel` on non-terminal plans with required reason and `Delete` only on Cancelled/Rejected plans.
4. Keep authoritative status codes, existing error feedback, disabled/busy states, and reload behavior.
5. Run the focused React test, lint, build, and full React test suite, then commit.

### Task 4: Add Farmer cancellation in Flutter

**Files:**
- Modify: `mobile/flutter_app/lib/services/api_client.dart`
- Modify: `mobile/flutter_app/lib/state/app_state.dart`
- Modify: `mobile/flutter_app/lib/models/api_models.dart` if a reusable cancellable-state getter is appropriate
- Modify: `mobile/flutter_app/lib/screens/planning_progress_screen.dart`
- Modify: `mobile/flutter_app/test/crop_plan_api_test.dart`
- Modify: `mobile/flutter_app/test/crop_plan_list_test.dart` or create `mobile/flutter_app/test/crop_plan_cancellation_test.dart`

1. Write failing API and widget tests for the cancel endpoint, confirmation, active-only visibility, refresh after success, failure feedback, and Cancelled rendering.
2. Run the focused Flutter tests and confirm the action/API method is missing.
3. Add the API client and AppState operation, then add the destructive confirmation action to plan progress for non-terminal plans.
4. Ensure Approved, Rejected, and Cancelled plans never expose cancellation.
5. Run focused tests, `flutter analyze`, and full `flutter test`, then commit.

### Task 5: Verify the integrated feature

**Files:**
- Review: all files changed above

1. Run `rg -n "^(<<<<<<<|=======|>>>>>>>)"` with generated/vendor exclusions.
2. Run `git diff --check`.
3. Run the required .NET restore/build/test commands from `AGENTS.md`.
4. Run `npm ci`, `npm run lint`, `npm run build`, and `npm test` in `frontend/react-app`.
5. Run `flutter pub get`, `flutter analyze`, and `flutter test` in `mobile/flutter_app`.
6. Review `git status`, commit any test-only or integration corrections locally, and do not push or change branches.
