# Pre-Planting Optional Evidence Submission Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow a valid linked pre-planting assessment to complete when an optional image upload fails, without changing backend contracts or workflow sequencing.

**Architecture:** Keep draft persistence and dedicated submission authoritative. Make only the image-upload portion of the React submit sequence best-effort, then verify the unchanged backend endpoint through a real HTTP integration test.

**Tech Stack:** React 19, TypeScript, Axios, Vitest, Testing Library, ASP.NET Core 8, EF Core 8, xUnit

---

### Task 1: Reproduce the frontend orchestration failure

**Files:**
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`

1. Add a test with a valid form, selected image, successful draft PUT, failed image POST, and successful dedicated submit POST.
2. Assert the current implementation reports the upload failure and never reaches the dedicated submit endpoint.
3. Run `npm test -- src/pages/PrePlantingAssessmentPanel.test.tsx` and confirm the new expectation fails before implementation.

### Task 2: Make optional evidence best-effort

**Files:**
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`
- Test: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`

1. Catch only the optional upload failure inside `submitAssessment`.
2. Call `POST /crop-plans/{id}/pre-planting-assessment/submit` regardless of that upload result.
3. On submit success, set the returned completed assessment, clear selected files, show success, and show a non-blocking optional-evidence warning when necessary.
4. On submit failure, keep the saved draft state/form and display the backend error.
5. Assert no-image, successful-image, failed-image, validation-error, read-only, cleared-file, and Run Field Analysis behavior.

### Task 3: Verify the existing backend HTTP contract

**Files:**
- Modify: `backend/AgriAssist.Api.Tests/PrePlantingAuthorizationIntegrationTests.cs`

1. Reuse the isolated HTTP test factory and authenticate as the seeded Field Officer.
2. Put a complete valid linked assessment and post the dedicated submit endpoint.
3. Assert HTTP 200, Completed status, UTC completion timestamp, unchanged inspection identity/count, and workflow step `CropFieldAnalysisAgent`.
4. Keep the existing role and generic endpoint authorization tests unchanged.

### Task 4: Verify containment and all affected projects

1. Run focused backend submission tests and the HTTP integration test.
2. Run the full backend test suite and Release build.
3. Run focused PrePlanting and Workflow Review React tests.
4. Run the full React test suite, lint, and production build.
5. Run `git diff --check` and scan for conflict markers.
6. Stage only the scoped source, test, and plan files; leave unrelated lockfiles untouched.
7. Commit locally as `fix: submit linked pre-planting assessment`. Do not push.
