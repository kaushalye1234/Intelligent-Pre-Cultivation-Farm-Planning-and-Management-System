# Field Analysis First-Claim Visibility Design

## Problem

When the crop-planning coordinator completes, the workflow waits at `CropFieldAnalysisAgent`, but no pre-planting `FieldInspection` exists yet. Field Officer workflow access currently requires an inspection already assigned to that officer. Because the inspection is created only when an officer saves the first assessment draft, no officer can discover or open a new farmer request through the workflow queue.

## Considered Approaches

1. **Shared unclaimed queue with first-save ownership (selected).** Show pending `CropFieldAnalysisAgent` workflows with no pre-planting inspection to every Field Officer. The first successful assessment draft creates the inspection and becomes its owner.
2. **Administrator assignment.** Require an Admin or Agricultural Officer to select a Field Officer before assessment work begins. This adds assignment state and a new management workflow that the current product does not otherwise require.
3. **Automatic assignment.** Choose a Field Officer when coordinator planning completes. The system has no workload, geography, or availability rules that could make that choice safely.

## Design

Change only `WorkflowApprovalService.ApplyAccess` for the Field Officer role. A Field Officer may see a workflow when either:

- a non-deleted inspection for the crop-plan request is already assigned to that officer; or
- the workflow is pending at `CropFieldAnalysisAgent` and no non-deleted pre-planting inspection exists for the crop-plan request.

The first condition preserves all existing owner access. The second condition exposes only active, unclaimed field-analysis work; it does not broaden access to other workflow stages, claimed assessments, or other roles.

No model or migration change is required. `SavePrePlantingAssessmentAsync` already writes `InspectorUserId` from the authenticated Field Officer. The filtered unique index on `FieldInspections.CropPlanRequestId` for `PrePlanting` inspections prevents two claims, while the existing `DbUpdateException` recovery returns `PREPLANT_ASSESSMENT_ALREADY_EXISTS` when another officer wins the race. After a successful claim, the unclaimed access branch becomes false and only the owning officer retains workflow access.

## User Flow

1. A farmer submits a crop-plan request and the coordinator advances it to pending field analysis.
2. Every Field Officer sees the workflow in **Tasks & Approvals > Workflow Review**.
3. A Field Officer opens the workflow and saves the assessment draft.
4. That save creates the linked pre-planting inspection and assigns it to the officer.
5. The workflow remains visible to the owner and disappears from other Field Officers' queues.
6. Existing assessment submission and downstream analysis behavior continues unchanged.

## Verification

Add service regression tests proving that an unclaimed pending field-analysis workflow is searchable and reviewable by a Field Officer, unrelated unclaimed stages remain hidden, and a claim transfers visibility exclusively to the owner. Retain the existing ownership and duplicate-claim tests, then run the focused and full backend test suites plus a Release build.
