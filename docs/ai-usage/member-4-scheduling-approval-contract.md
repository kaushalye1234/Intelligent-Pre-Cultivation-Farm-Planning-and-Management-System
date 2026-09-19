# Member 4 Scheduling Validation and Approval Contract

`SchedulingValidationAgent` is the fourth crop-planning step. It reads the persisted coordinator, field-analysis, and weather/resource outputs for one workflow and candidate revision. It proposes work only; it cannot approve or write tasks, irrigation schedules, or reservations.

## Run the scheduling step

```http
POST /api/task-approval/workflows/{workflowId}/generate-candidate
```

Roles: AgriculturalOfficer or Admin.

ASP.NET sends the bounded workflow context and stored upstream outputs to:

```http
POST /workflows/crop-planning/scheduling-validation
```

A ready result has `status: "CandidateReady"`, the current `candidateRevision`, `requiresHumanApproval: true`, candidate tasks and irrigation schedules, warnings, estimated cost when authoritative costs exist, and explicit constraints. Missing or mismatched upstream outputs produce `MissingDependency`, require human review, and contain no candidates.

The backend validates identifiers, ownership, revision, dates, conflicts, inventory, reservations, and budget using current database state. Only a valid candidate moves the workflow to `PendingOfficerApproval`. The AI result cannot set its own eligibility.

## Review and decisions

```http
GET  /api/task-approval/workflows
GET  /api/task-approval/workflows/{workflowId}
GET  /api/task-approval/workflows/{workflowId}/history
POST /api/task-approval/workflows/{workflowId}/approve
POST /api/task-approval/workflows/{workflowId}/reject
POST /api/task-approval/workflows/{workflowId}/request-revision
```

Decision body:

```json
{
  "candidateRevision": 1,
  "expectedWorkflowVersion": 3,
  "idempotencyKey": "client-generated-unique-key",
  "comment": "Officer decision reason"
}
```

Stale revisions or versions return `409`. Replaying the same successful decision with the same idempotency key returns the existing result. Reusing the key for another decision returns `409`. Reject and revision require a reason, revisions are limited to three, and neither action creates final work.

Approval revalidates mutable data and commits the decision, approved tasks, approved irrigation schedules, resource reservations, crop-plan status/history, and workflow completion in one relational transaction. Workflow-generated items cannot be approved through the older item-level endpoints.

## Review UI

The React queue is available under `/task-approval`; a workflow opens at `/task-approval/workflows/{workflowId}`. It displays all stored step outputs, validation errors and warnings, revision/version values, and decision history. Only AgriculturalOfficer and Admin users receive decision controls.

## Verification boundary

The xUnit tests use EF InMemory for deterministic service behavior. PostgreSQL transaction rollback and competing-request serialization still require an isolated PostgreSQL integration run before production claims are made.
