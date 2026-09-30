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

## Version-2 evidence and proposal

The current request includes `contractVersion: 2` and an `evidence` bundle built by ASP.NET from the same persisted workflow. It carries the pinned active profile ID, source name/version/verification time, completed Member 1-3 step IDs, ordered crop stages, and parsed `IrrigationSchedule` rules. `invalidIrrigationRuleIds` makes malformed persisted rules explicit. If Member 3 names a `requirementSource.cropReferenceProfileId`, the bundle must use that exact profile; the scheduler cannot substitute another profile.

Each candidate task, irrigation entry, and reservation includes `reason` and one to four `sources`. A source has `kind`, stable `id`, `label`, and optional `profileId`, `sourceVersion`, `verifiedAt`, and `sourceUrl`. Allowed kinds are `FieldAnalysis`, `CropStage`, `IrrigationRule`, and `ResourceRequirement`. The backend independently compares these references with current persisted evidence before a ready result can enter officer approval, and again during approval.

The agent creates preparation review tasks only from Member 2 `fieldPreparationRequirements`, stage review tasks only from the selected verified profile, irrigation only from its parsed rules, and reservations only from Member 3 sufficient requirements and uniquely matched stock rows. A profile with no verified irrigation rule yields `candidateIrrigation: []`; neither duration nor water need is invented. Cost remains `null` without verified prices. All dates remain inside the selected window and timestamps are UTC.

`CandidateBlocked` (`AgentWorkflowStatus` 12) preserves the proposed items, warnings, constraints, and failed validation for review while setting `requiresHumanApproval: false`. A verified shortage, high weather risk, unsupported rule, ambiguous stock, or impossible date blocks approval. `Unknown` weather is a warning if other checks pass. Missing upstream/profile/stage evidence produces `MissingDependency` with no candidates. A blocked workflow must run a **new upstream workflow** after stock, weather, or reference data changes; regenerating from the old Member 3 snapshot is not a refresh.

The version-2 result uses the same endpoint and includes `contractVersion: 2`. Older stored results without that property remain readable and use the existing approval revalidation path. Only a version-2 result receives the new reason/source and zero-irrigation checks.

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

The React queue is available under `/task-approval`; a workflow opens at `/task-approval/workflows/{workflowId}`. It displays sourced proposal items and blocking reasons before raw stored output, validation results, revision/version values, and decision history. Only AgriculturalOfficer and Admin users receive decision controls. Source URLs become links only for `http` and `https` schemes. Flutter labels blocked workflows and states that no farm work has been approved.

## Verification boundary

The xUnit tests use EF InMemory for deterministic service behavior. PostgreSQL transaction rollback and competing-request serialization still require an isolated PostgreSQL integration run before production claims are made.
