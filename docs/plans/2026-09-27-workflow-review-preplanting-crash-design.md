# Workflow Review Pre-Planting Crash Design

## Scope

Keep Workflow Review mounted while its linked pre-planting assessment is loaded, displayed, edited, or fails. Preserve the existing crop-plan request linkage, workflow state, agent sequence, permissions, approval behavior, and page design.

## Root cause

`PrePlantingAssessmentPanel` treats its TypeScript response types as runtime guarantees. After the requests resolve it stores the raw JSON and immediately renders it. The post-load tree directly evaluates `assessment.images.length`, `assessment.images.map`, `result.fieldCondition.summary`, and `result.warnings.map`; `applySavedAssessment` also evaluates `assessment.identifiedRisks.length`. A successful legacy or malformed response with an omitted or null collection/nested object therefore throws during or immediately after `setState`. There is no error boundary around the panel, so React unmounts the route tree and the browser shows a blank page.

The request-level `try/catch` cannot contain exceptions thrown during the later React render. It also groups context, assessment, and optional result requests in one `Promise.all`, so a 404 for an assessment that does not exist yet is treated like a fatal load failure instead of the expected empty state.

The current ASP.NET response builder normally emits `images` as an array and supports `identifiedRisks` as either `null` or an array. The fix therefore belongs at the frontend runtime boundary and does not require an API or authorization change.

## Design

Add small, local normalizers beside `PrePlantingAssessmentPanel`. They will validate the minimum identity fields of a non-null assessment, coerce optional text fields to `null`, default `images` to an empty array, and preserve the semantic distinction between an unassessed `identifiedRisks: null` and an assessed empty list `identifiedRisks: []`. Field-analysis output will receive safe defaults for optional collections and its nested field-condition object.

Load the context, assessment, and optional field-analysis result independently. An assessment 404 becomes `null`. Other request failures become a readable panel error. Successfully loaded data remains usable when a sibling optional request fails. A malformed successful payload becomes a readable contract error rather than entering render state.

Add a small error boundary immediately around the assessment panel on Workflow Review. It is final containment for unexpected child-render exceptions and will render the same existing error-state component. The Agent Evidence, deterministic validation, and decision history sections remain siblings and continue rendering.

No shared API client, backend DTO, database model, route, permission, workflow status, revision/version logic, or approval action will change.

## Role behavior

- Field Officer: may create, edit, save, upload, submit, and run field analysis only under the existing workflow-state and ownership rules.
- Agricultural Officer and Admin: retain their current read-only view.
- Resource Officer and Farmer: the raw assessment panel remains hidden and makes no raw-assessment requests.
- Approval permissions remain controlled by the existing `isDecisionRole` path and are untouched.

## Error and empty states

- HTTP 404 from the assessment endpoint: render the existing Field Officer form or the existing read-only waiting notice.
- HTTP 500 or another load failure: keep the panel shell and display a readable in-panel error.
- Malformed successful payload: reject it before state update and display an in-panel contract error.
- Unexpected render exception: the local boundary displays an error inside the panel location while the rest of Workflow Review remains mounted.

## Verification

Add focused component and route tests for valid and missing assessments, null optional values, 404, 500, malformed JSON, Field Officer editing, Agricultural Officer/Admin read-only behavior, Resource Officer behavior, and Agent Evidence survival when the boundary catches a panel exception. Run the focused tests, the full frontend test suite, lint, production build, `git diff --check`, and a conflict-marker scan.
