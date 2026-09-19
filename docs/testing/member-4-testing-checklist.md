# Member 4 testing checklist

This checklist verifies farm tasks, irrigation schedules, the scheduling-validation agent, human approval, and farmer status visibility.

## Automated checks

From the repository root:

```powershell
$env:Logging__EventLog__LogLevel__Default='None'
dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj --configuration Release
```

```powershell
cd ai-service
.venv\Scripts\python.exe -m pytest
```

```powershell
cd ..\frontend\react-app
npm run lint
npm run build
npm test
```

```powershell
cd ..\..\mobile\flutter_app
flutter analyze
flutter test
```

## Service checks

1. PostgreSQL contains the five applied migrations, including `AddSchedulingApprovalWorkflow`.
2. `GET http://localhost:8001/health` returns HTTP 200.
3. An AI request without a bearer token returns HTTP 401.
4. The same request with the configured token reaches schema validation instead of returning 401.
5. `GET http://localhost:5087/health` returns HTTP 200.

## Workflow acceptance test

1. Create a crop-plan request and complete compatible Member 1, Member 2, and Member 3 workflow outputs.
2. As an AgriculturalOfficer or Admin, call `POST /api/task-approval/workflows/{id}/generate-candidate`.
3. Confirm the response contains candidate tasks and/or irrigation schedules, validation constraints, warnings, and `requiresHumanApproval=true`.
4. Confirm the workflow becomes `PendingOfficerApproval` only when deterministic validation succeeds.
5. Approve with a unique idempotency key and the expected workflow version.
6. Confirm the approval creates final tasks, irrigation schedules, reservations, decision history, and completed workflow state atomically.
7. Replay the same approval request and confirm it returns the original decision without duplicates.
8. Submit a stale version or competing decision and confirm HTTP 409 with no partial writes.
9. Reject or request revision with a real officer comment and confirm the candidate does not create final work.
10. Log in to Flutter as the owning farmer and confirm **My status** shows task status, irrigation status, and approval history.

For a real pending workflow, run the API concurrency probe with its candidate revision, workflow version, and an authorized officer token:

```powershell
.\scripts\test-member4-api-concurrency.ps1 -WorkflowId <workflow-id> -CandidateRevision 1 -ExpectedWorkflowVersion <version> -BearerToken <officer-token>
```

It must report one successful approval and one HTTP 409 conflict. Never use a production token or database for this probe.

## PostgreSQL evidence

Run the repeatable database smoke checks from the repository root:

```powershell
.\scripts\test-member4-postgres.ps1
```

This verifies migration history, required Member 4 tables, rollback of a temporary transaction, and row-lock support. It does not replace API-level concurrent approval tests.

Record the database target category, migration IDs, test count, approval outcomes, rollback result, and any concurrency failures. Do not use EF InMemory results as evidence for PostgreSQL transaction or concurrency guarantees. Do not record credentials or fabricate latency numbers.
