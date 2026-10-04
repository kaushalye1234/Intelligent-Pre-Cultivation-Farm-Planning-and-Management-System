# Field Analysis First-Claim Visibility Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make pending, unclaimed field-analysis workflows discoverable to every Field Officer while preserving first-save ownership and duplicate-claim protection.

**Architecture:** Extend the existing role-scoped workflow query in `WorkflowApprovalService` with one narrowly bounded unclaimed-work predicate. Keep assessment creation and ownership in `CropPlanningService`; its authenticated owner assignment, filtered unique index, and concurrency recovery remain authoritative.

**Tech Stack:** ASP.NET Core 8, EF Core 8, PostgreSQL, xUnit

---

### Task 1: Lock the visibility contract with failing tests

**Files:**
- Modify: `backend/AgriAssist.Api.Tests/WorkflowApprovalTests.cs`

**Step 1: Add a Field Officer service fixture**

Allow the existing `NewService` helper to accept an `ApplicationRole`, defaulting to `AgriculturalOfficer`, so access tests can exercise the production role predicate without duplicating service construction.

**Step 2: Add the unclaimed visibility test**

Seed a pending workflow at `CropFieldAnalysisAgent` with no linked pre-planting inspection. Assert a Field Officer receives it from `SearchAsync` and can load it through `GetAsync`.

**Step 3: Add the stage-boundary test**

Leave an otherwise unclaimed workflow at `SchedulingValidationAgent`. Assert a Field Officer receives no search result and `GetAsync` returns the existing not-found behavior.

**Step 4: Add the post-claim ownership test**

Create a linked pre-planting `FieldInspection` owned by one Field Officer. Assert the owner can search and open the workflow while a second Field Officer can do neither.

**Step 5: Run the focused tests and confirm the new unclaimed test fails**

Run:

```powershell
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~WorkflowApprovalTests" --no-restore
```

Expected: the unclaimed pending workflow test fails before the service predicate changes; existing ownership behavior remains green.

### Task 2: Implement the minimal role-scoped access change

**Files:**
- Modify: `backend/AgriAssist.Api/Services/TaskApproval/WorkflowApprovalService.cs`

**Step 1: Name the field-analysis agent constant**

Add `FieldAnalysisAgentName = "CropFieldAnalysisAgent"` beside the existing workflow agent constants.

**Step 2: Extend only the Field Officer predicate**

Preserve access through an inspection assigned to the current officer. Add an alternative requiring all of the following:

- the workflow has a crop-plan request;
- workflow status is `Pending`;
- current step is `CropFieldAnalysisAgent`; and
- no non-deleted `PrePlanting` inspection exists for that crop-plan request.

Do not change Farmer, Resource Officer, Agricultural Officer, or Admin access.

**Step 3: Run focused workflow and ownership tests**

Run:

```powershell
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~WorkflowApprovalTests|FullyQualifiedName~CropPlanningAiWorkflowTests" --no-restore
```

Expected: all tests pass, including the existing other-officer ownership rejection.

### Task 3: Verify the backend and repository

**Files:**
- No source changes expected

**Step 1: Build the backend tests project**

```powershell
dotnet build backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-restore
```

Expected: build succeeds without new warnings or errors.

**Step 2: Run the full backend suite**

```powershell
$env:Logging__EventLog__LogLevel__Default = 'None'
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-build
```

Expected: all non-PostgreSQL tests pass; PostgreSQL-only tests may remain skipped when their isolated connection string is not configured.

**Step 3: Check repository hygiene**

Run `git diff --check` and scan tracked source for conflict markers. Confirm the pre-existing React lockfile and Vite configuration changes remain untouched.

**Step 4: Commit the focused fix locally**

Stage only `WorkflowApprovalService.cs` and `WorkflowApprovalTests.cs`, then create a local commit. Do not change branches or push.
