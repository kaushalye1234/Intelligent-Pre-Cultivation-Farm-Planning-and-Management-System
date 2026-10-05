# Crop Plan Cancellation and Archive Design

## Goal

Allow a farmer to withdraw an active crop plan and create a replacement, while allowing Crop Planning Admins to cancel on the farmer's behalf and remove terminal requests from normal administration lists without destroying workflow or audit evidence.

## Lifecycle Rules

- Cancellation is allowed from every active, non-terminal crop-plan workflow stage.
- Cancellation is blocked when the request is `Approved`, `Rejected`, `Cancelled`, or already archived.
- The owning Farmer and an Admin may cancel.
- A Farmer confirms cancellation; the history record uses a standard farmer-cancellation reason.
- An Admin must provide a non-empty reason. The reason is trimmed, length-limited, and stored with the actor's user ID, role snapshot, and UTC timestamp.
- `Cancelled` remains a terminal request and workflow state.
- Cancelling an active request releases the existing duplicate-active-plan restriction so the farmer may submit a replacement.

## Cancellation Transaction

Cancellation updates the plan lifecycle atomically:

1. Recheck access, current request status, and archival state.
2. Set the request status to `Cancelled` and update its audit fields.
3. Add a crop-plan history entry with actor, role, timestamp, action, and reason.
4. Set the latest non-terminal agent workflow to `Cancelled`, set `CurrentStep` to `Cancelled`, set `CompletedAt`, and increment its concurrency version.
5. Mark pending or running agent steps as `Skipped` with a cancellation error code while retaining their inputs, outputs, tool executions, and completed steps.
6. Mark an unfinished linked pre-planting inspection as `Cancelled` while retaining observations, images, image analyses, reviews, and decisions.

The workflow version change invalidates an in-flight agent lease. Every long-running AI completion path must recheck the persisted workflow lease and request status before applying its output, so a result arriving after cancellation cannot reactivate the request.

Approved plans are not cancellable because approved tasks, schedules, or reservations may already exist. Rejected and previously cancelled plans are already terminal and remain immutable.

## Admin Removal

`DELETE /api/crop-planning/requests/{id}` is an Admin-only archive operation despite using the conventional HTTP delete verb.

- Only `Cancelled` or `Rejected` requests may be archived.
- Active, approved, or already archived requests are rejected with a conflict response.
- Archiving sets the request's `IsDeleted` flag and audit fields and adds a same-status archive history entry.
- It never physically deletes the request or any workflow, steps, inspections, evidence, decisions, or history.
- Archived requests disappear from normal Farmer and Admin request lists and cannot be opened through normal request endpoints.

## API Contract

`POST /api/crop-planning/requests/{id}/cancel`

```json
{
  "reason": "Required for Admin; optional for Farmer"
}
```

The response is the updated crop-plan request. Validation and conflict errors use the existing structured API error format.

`DELETE /api/crop-planning/requests/{id}` returns `204 No Content` after a successful soft archive.

Crop-plan history responses expose the recorded action, reason, and role snapshot in addition to the existing actor and timestamp fields. Existing history rows remain readable with nullable values for the new fields.

## Interfaces

### Flutter Farmer App

- Show a `Cancel plan` action on plan progress for any non-terminal request.
- Require a destructive confirmation before sending the cancellation request.
- Refresh plan and workflow state after success and render the existing `Cancelled` label.
- Hide the action for Approved, Rejected, and Cancelled plans.

### React Admin

- Show `Cancel` for active requests.
- Use a confirmation dialog containing a required reason field.
- Show `Delete` only for Cancelled or Rejected requests.
- Explain in the delete confirmation that the request is removed from normal lists while audit history is retained.
- Reload the table after either action.

The controls follow the existing application component, spacing, icon, feedback, and error-handling conventions.

## Verification

Backend tests cover role and ownership rules, all permitted and blocked states, Admin reason validation, audit contents, workflow/step/inspection transitions, evidence preservation, soft archival, replacement-plan creation, and in-flight concurrency protection.

React tests cover action visibility, required Admin reason, API calls, refresh behavior, and archive confirmation. Flutter tests cover action visibility, confirmation, API use, refresh behavior, and terminal-state rendering. The affected .NET, React, and Flutter suites are run before finalizing local commits.
