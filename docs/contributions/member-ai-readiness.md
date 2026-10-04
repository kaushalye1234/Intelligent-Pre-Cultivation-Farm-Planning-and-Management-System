# Member AI Readiness

The BASIC foundation is intended to support four later AI member contributions. Real AI remains out of scope until those prompts.

## Member 1 - CropPlanning AI

Build on CropPlanRequest, CropPlanRequestHistory, CropType, Farm, Field, and AgentWorkflow.

## Member 2 - Inspection AI

Build on FieldInspection, CropIssue, FollowUpRecommendation, InspectionImage, and AgentWorkflow.

## Member 3 - Resource AI

Build on Resource, InventoryStock, StockTransaction, ResourceReservation, and AgentWorkflow.

## Member 4 - TaskApproval AI

### Component profile

**Primary responsibility:** farm tasks, irrigation schedules, scheduling validation, officer approval/revision, and integration of the scheduling result with the farmer's status view.

**Business-specific operation:** create a source-linked scheduling proposal from persisted Member 1-3 results, validate it, and pause for an Agricultural Officer/Admin decision. Approval rechecks mutable inventory and scheduling constraints before final records are created. A blocked proposal remains reviewable but cannot be approved.

### Assignment evidence map

| Assignment requirement | Member 4 evidence in this repository | Status / honest limit |
| --- | --- | --- |
| ASP.NET backend; at least four meaningful endpoints and one business operation (Sections 3 and 5) | `TaskApprovalController` exposes workflow list/review/history, candidate generation, approve/reject/revision, task operations, irrigation schedule operations, and approval history. OpenAPI snapshot lists 22 `/api/task-approval/*` paths. | Endpoint-count requirement is exceeded. Use the controller and OpenAPI snapshot as evidence; confirm personal authorship against Git history. |
| Database models, relationships, migrations and integrity (Section 6) | `FarmTask`, `IrrigationSchedule`, `ApprovalDecision`, their `AppDbContext` mappings, and EF migrations/snapshot. Approval decisions reference the task/schedule and workflow. | Implemented in the integrated project. PostgreSQL-specific claims should be supported by the disposable-PostgreSQL verification log, not InMemory tests. |
| React contribution (Section 7) | Protected `/task-approval` queue and `/task-approval/workflows/:id` review page show workflow evidence and support candidate generation and officer decisions. | Implemented; cite React tests and PR/CI evidence when assembling the final report. |
| Flutter contribution (Section 8) | Farmer status screen reads tasks, irrigation schedules and workflow/approval status through the shared ASP.NET API. | Implemented; cite Flutter tests/build evidence. |
| Distinct Agentic AI contribution (Sections 9 and 16) | `SchedulingValidationAgent` consumes persisted upstream results and deterministically produces source-linked task, irrigation and reservation proposals. The current Member 4 feature branch adds an optional single-tool OpenAI call for `GetVerifiedCropProfile`, backed only by existing read-only GET tools. | Implemented and locally tested on `member4/render-phase4-clean`; it remains disabled by default and is not yet merged or deployed. Do not claim a live model/provider run. The deterministic scheduler remains the sole source of candidate content. |
| Controlled tools, validation, safe failures, state and human approval (Sections 9.1 and 17) | The integrated group workflow persists workflow/step/candidate/decision state, validates proposal evidence, and gates final writes behind authorized approval. The officer UI presents source/reason information; approval rechecks mutable constraints. | The retrieval can only read a profile and cannot create farm data or approvals. Its code does not replace the assessed group workflow; final writes remain gated by officer authorization. |
| Tests, CI and Git ownership (Sections 3, 16 and 17) | Member 4 backend/AI/React/Flutter tests, PostgreSQL smoke/concurrency evidence, and implementation/report PR history are recorded in `docs/testing/verification-log.md`, the consolidated report draft, and PR #59/#60. | Test results are dated evidence, not a promise that CI is currently green. Confirm the commits are yours and link the passing CI runs in the final report. |
| Individual AI log and reflection (Sections 18.3 and 19) | Starter log: `docs/contributions/member-4-ai-usage-log.md`. | You must reconcile all earlier AI use, write the approximately one-page reflection in your own words, and sign your own declaration. Do not copy an AI-written reflection. |

### Confirmed individual Git evidence

The student confirmed that `Kaushalye` is their Git author identity. These relevant Member 4 commits are recorded under that author. The list is evidence of the student's identifiable contribution; shared commits and changes by other authors remain separate.

| Commit | Date | Recorded change |
| --- | --- | --- |
| `c4ea2ba` | 2026-09-17 | Implement scheduling approval workflow |
| `6dc27aa` | 2026-09-17 | Add farmer workflow status view |
| `522bcc8` | 2026-09-30 | Add evidence-linked scheduling proposals |
| `14f7b41` | 2026-10-01 | Bind explanations to verified evidence |
| `5f79910` | 2026-10-01 | Align irrigation-rule explanation length |

PR #59 is recorded in the project report as the integrated Member 4 implementation PR. Cite these commits and PR #59 as the Member 4 evidence; do not present every shared or imported commit as individual work.

### Simple viva explanation

“My component turns verified crop, field, weather and resource evidence into a scheduling proposal. The scheduler validates the evidence and explains each proposed task or irrigation entry. The API keeps the proposal pending until an authorized officer approves it; only then does the backend recheck the current constraints and create final records. The farmer can then see the resulting status in Flutter.”

### Evidence pointers

- Backend API: `backend/AgriAssist.Api/Controllers/TaskApproval/TaskApprovalController.cs`
- Data model/mapping: `backend/AgriAssist.Api/Models/TaskApproval/`, `backend/AgriAssist.Api/Data/AppDbContext.cs`, and `backend/AgriAssist.Api/Migrations/`
- Agent: `ai-service/agents/scheduling_validation_agent.py`, `ai-service/agents/evidence_scheduler.py`, and `ai-service/schemas/scheduling_validation.py`
- React: `frontend/react-app/src/pages/TaskApprovalPage.tsx` and `WorkflowReviewPage.tsx`
- Flutter: `mobile/flutter_app/lib/screens/status_screen.dart` and `mobile/flutter_app/lib/services/api_client.dart`
- Tests and limits: `docs/testing/member-4-testing-checklist.md` and `docs/testing/verification-log.md`
- Report assembly: `docs/reports/group-04-consolidated-report-draft.md`

## Guardrail

Do not bypass manual approval, RBAC, validation, or backend service rules when adding AI.

## Current branch handoff - 2026-10-05

The clean `member4/render-phase4-clean` branch is based on `origin/dev` at `bb3bd9d` and contains the bounded retrieval feature, its tests, and Render demo preparation. Backend xUnit passed 270 tests with 15 PostgreSQL-only tests skipped; React lint/build/tests passed (109 React tests); the full AI suite passed 186 tests using an isolated Python 3.14 environment with Pydantic 2.12.4 because the available system Python 3.14 cannot install the repository's pinned Pydantic 2.10.4 native core. These local checks are supplemented by passing backend, AI-service, React, and Flutter GitHub Actions jobs on commit `b5a6a29`. The branch is pushed and open as draft PR #67; it remains unmerged and undeployed.
