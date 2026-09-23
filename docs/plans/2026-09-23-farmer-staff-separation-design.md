# Farmer Mobile and Staff Field-Analysis Separation Design

## Goal

Make Flutter a Farmer-only application while completing the Field Officer's pre-planting field-analysis stage in the existing React staff workflow. Keep the existing crop-planning workflow, inspection evidence, AI agent, resource operations, and approval flow intact.

## Architecture

Flutter accepts only Farmer sessions. It exposes Dashboard, Plans, and My Status and does not initialize inspection or resource-management state. Staff authentication is rejected with a direction to use the React Staff Portal.

React continues to use `/task-approval/workflows/:id`. When a workflow is waiting on `CropFieldAnalysisAgent`, Field Officers receive a structured pre-planting assessment panel. Agricultural Officers and Admins may view saved assessment evidence, but they do not receive assessment mutation controls. Resource Officers remain outside this stage.

The assessment reuses `FieldInspection`, `InspectionObservation`, and `InspectionImage`. `FieldInspection` receives an optional crop-plan request link and a purpose enum. A filtered unique index allows one `PrePlanting` inspection per crop-plan request while leaving existing unlinked inspections valid.

## Data model and migration

`FieldInspection` gains `Guid? CropPlanRequestId`, a `CropPlanRequest?` navigation, and `InspectionPurpose Purpose`. Purpose values are `Routine = 1` and `PrePlanting = 2`.

The migration adds a nullable foreign key, a non-null purpose column defaulted to `Routine`, and a filtered unique index on `(CropPlanRequestId, Purpose)` when the request ID is not null. Existing rows retain null links and remain routine inspections.

## Assessment API

The crop-planning workflow controller adds:

- `GET /api/crop-plans/{id}/pre-planting-assessment` for Field Officer, Agricultural Officer, and Admin viewing.
- `PUT /api/crop-plans/{id}/pre-planting-assessment` for Field Officer draft creation and editing.

The request contains only soil condition, water availability, irrigation availability, drainage condition, general field condition, planting readiness, risks or concerns, and officer notes. The service upserts one linked `FieldInspection` and typed `InspectionObservation` rows. Existing inspection image and submission endpoints remain authoritative for evidence upload and completion.

Before the existing field-analysis endpoint runs, it requires that exact linked assessment to be submitted. The React UI invokes it only for the Field Officer after save, evidence upload, and submission succeed.

## Exact AI evidence

`FieldAnalysisInput` carries the crop-plan request ID and linked pre-planting inspection ID. The Python schema mirrors these fields. Existing inspection tool endpoints are scoped to the linked `PrePlanting` inspection for inspection, issue, and image evidence. Output validation accepts evidence IDs only from that assessment.

This preserves the agent and avoids ambiguity from unrelated historical inspections on the same field. No duplicate workflow or assessment module is introduced.

## React behavior

The Workflow Review page adds a page-local panel that loads the linked assessment, shows edit controls only to a Field Officer while the step is actionable, saves before uploading selected evidence, submits only after all prior operations succeed, then calls field analysis and reloads the workflow/result. Agricultural Officer/Admin see a read-only assessment and result.

Errors identify the failed stage and preserve the saved draft for retry. Duplicate clicks are disabled.

## Flutter behavior

The mobile shell contains Dashboard, Plans, and My Status only. Inspection and resource-management screens, state, API methods, models, and device dependencies are removed when no Farmer feature consumes them. Farmer tasks, irrigation, approval status, farm, field, and crop-planning data remain.

Login and restored-session flows reject non-Farmer roles, clear staff tokens, and show `This account is for staff. Please use the React Staff Portal.` `AuthGate` also prevents a staff profile from reaching `HomeShell`.

## Verification

Tests cover backward-compatible inspection persistence, role enforcement, exact linked evidence, run-before-submit rejection, React operation ordering/read-only roles, Farmer-only Flutter navigation/session rejection, and preserved Farmer crop-plan/status behavior. All backend, AI, React, and Flutter checks required by `AGENTS.md` run before local commits.
