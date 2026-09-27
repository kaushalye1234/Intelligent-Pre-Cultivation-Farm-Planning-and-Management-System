# Workflow Review Pre-Planting Crash Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Keep Workflow Review usable when linked pre-planting data is absent, fails, or has nullable/legacy fields.

**Architecture:** Normalize untrusted assessment and field-result JSON locally before React state updates. Load the assessment independently so its 404 is an empty state, and wrap only that panel in an error boundary so Agent Evidence remains mounted.

**Tech Stack:** React, TypeScript, Axios, Vitest, Testing Library, Vite

---

### Task 1: Reproduce response failures

**Files:**
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`

1. Add failing tests for a valid assessment, no assessment, nullable optional fields, omitted/null collections, HTTP 404, HTTP 500, and malformed success data.
2. Assert Field Officer edit controls and existing read-only role behavior.
3. Run `npm test -- src/pages/PrePlantingAssessmentPanel.test.tsx`; expect the unsafe collection cases to fail before the fix.

### Task 2: Normalize and load safely

**Files:**
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`
- Test: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`

1. Add local runtime normalizers. Preserve `identifiedRisks: null` as unassessed and `[]` as explicitly assessed with no risks; default absent images and optional result collections to empty arrays.
2. Validate the minimum assessment identity fields and reject malformed success payloads with a readable contract error.
3. Settle context, assessment, and optional field-result loads independently. Convert only assessment 404 to `null`; display other errors in the panel.
4. Guard all remaining collection and nested-object rendering without changing forms, permissions, or workflow-state gates.
5. Run the focused panel tests; expect all to pass.

### Task 3: Contain unexpected render exceptions

**Files:**
- Modify: `frontend/react-app/src/pages/WorkflowReviewPage.tsx`
- Modify: `frontend/react-app/src/pages/WorkflowReviewPage.test.tsx`

1. Add a local class error boundary using the existing `ErrorState` and wrap only `PrePlantingAssessmentPanel`.
2. Add a test that forces that child to throw and verifies its fallback plus Agent Evidence are both visible.
3. Retain the decision test to prove approval data and permissions are unchanged.
4. Run both focused test files; expect all to pass.

### Task 4: Verify and commit

1. Run `npm test`.
2. Run `npm run lint`.
3. Run `npm run build`.
4. Run `git diff --check` and scan for conflict markers.
5. Commit only the scoped implementation and test files locally as `fix: contain pre-planting review failures`. Do not push.

### Task 5: Accept the endpoint's empty 204 representation

**Files:**
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`

1. Add a failing regression test whose assessment request resolves with HTTP 204 and an empty response body.
2. Assert the Field Officer sees the new-assessment form, including `Save draft` and `Submit assessment`, with no invalid-response error.
3. Treat only HTTP 204, `null`, `undefined`, and an empty response body as no assessment; continue validating every non-empty payload through `normalizeLinkedAssessment`.
4. Re-run the focused panel tests, full React tests, lint, production build, and `git diff --check`.
5. Commit the regression fix locally only after all required verification passes. Do not change the backend contract or push.
