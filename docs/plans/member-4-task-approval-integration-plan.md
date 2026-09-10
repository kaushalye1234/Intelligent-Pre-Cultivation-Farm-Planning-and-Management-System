# Member 4: Task/Approval and Final Integration Plan

Date: 2026-09-09
Status: Milestone 1 manual Task/Approval hardening implemented and merged with `origin/main` foundation commit `71f5d17` on 2026-09-10; shared AI and four-agent integration remain pending.
Assignment source: `05_FINAL_MEMBER_4_TASK_APPROVAL_INTEGRATION.md`, supplied by the user from Downloads.

## Progress update — 2026-09-10

Milestone 1 now includes task/schedule detail APIs, schedule update, draft/revision submission, auditable cancellation, restricted soft deletion, scoped filters/history, stable sorting, date/reference validation, task-time and irrigation-overlap conflict checks, and server-owned approval transitions. The React page now supports editing, resubmission, cancellation, and officer-entered decision reasons. Cancellation reuses immutable `ApprovalDecision` records with a new append-only `Cancelled = 4` enum value, so this milestone does not require a schema migration.

Verified locally: backend build passed with no warnings; seven focused Task/Approval and regression tests passed; React production build passed; 12 frontend tests passed; lint completed with existing hook/fast-refresh warnings and no errors. The full backend suite passed 10 of 11 tests, with the unrelated auth integration test blocked by Windows Event Log permissions in the sandbox. PostgreSQL concurrency and the four-agent workflow remain unverified and are part of later milestones.

After that verification, the Member 4 branch pulled the complete foundation from `origin/main`. The combined tree now includes the ASP.NET Core project, React application, shared workflow models, and `mobile/flutter_app`. The combined backend build, seven focused tests, frontend lint, frontend production build, and all 12 frontend tests pass. The separate `ai-service` and the persisted outputs/contracts from Members 1-3 are still absent, so M2-M5 cannot be implemented faithfully yet. Flutter checks remain unavailable because the Flutter SDK is not installed in this environment.

## 1. Your responsibility

You own Task/Approval and SchedulingValidationAgent, the deterministic validation and human approval gate, and coordination of the final integrated demonstration. You integrate the other three members' outputs; you do not take ownership of rebuilding their agents or the shared AI infrastructure.

| Responsibility | Your deliverable | Dependency |
| --- | --- | --- |
| Task/Approval business module | Complete task/schedule CRUD, queries, conflicts, history and authorization | Existing .NET models and services |
| SchedulingValidationAgent | Structured candidate tasks/irrigation and constraints, with read-only allow-listed tools | Shared AI service, graph and Members 1-3 outputs |
| Deterministic validation | Persisted validation tied to a candidate revision | Authoritative farm, crop, date, budget and resource data |
| Human approval | Officer approval/rejection/revision, with state and concurrency checks | Shared workflow contract |
| Final execution | Atomic tasks, schedules, reservations, decision, statuses and audit | Member 3 transaction-compatible reservation service |
| React workflow review | A single review page showing the full evidence and decision actions | Workflow review API and shared React conventions |
| Farmer final status | Owner-scoped status/history/tasks/irrigation UI integration | Team Flutter source and agreed state management |
| Integration evidence | E2E tests, performance report, ERD, ADRs and submission checklist | Completed teammate contributions and real test environment |

Direct existing files are under `backend/AgriAssist.Api/{Controllers,Services,Dtos,Validators,Models}/TaskApproval` and `frontend/react-app/src/pages/TaskApprovalPage.tsx`. Shared edits include AppDbContext/migrations, workflow models, DI, React routing/types, and resource service integration. Coordinate those shared edits with their owners; do not copy all shared project files into a purported Member 4-only contribution.

## 2. Verified current state and gaps

The initial local folder reviewed for this plan was a foundation snapshot rather than the complete post-Prompts-01-04 integration base, and it was not itself a Git checkout. The contribution is now tracked on `member4/task-approval-foundation` in the team repository and has pulled foundation commit `71f5d17` from `origin/main`. The merged branch includes `mobile/flutter_app`, but `ai-service/` is still absent and the AgenticAI client remains disabled. The existing shared workflow model and steps must be reused and extended when the missing team AI contracts arrive, rather than duplicated.

| Area | Present | Gap to address |
| --- | --- | --- |
| Tasks | Search/pagination, create, update, individual decisions | Detail/delete, richer filtering/sorting, conflict rules, transition history |
| Irrigation | Paginated list, create, individual decisions | Detail/update/delete, filtering, overlap checks and history |
| Approval authorization | Controller and decision-service checks allow AgriculturalOfficer/Admin | Tighten all alternative status-changing paths; preserve farmer access boundaries |
| Request validation | Required IDs, lengths, positive irrigation duration | Valid dates/enums, required rejection/revision reasons, relevant relationship checks |
| Status changes | Decision methods require pending state | Create/update accepts caller-supplied status; enforce server-owned approval transitions |
| Approval history | Global decision list | Scope farmer visibility to authorized farms/tasks/workflows |
| Duplicate approval | Sequential repeat is blocked and has an InMemory regression test | Concurrent requests and workflow-level idempotency are not established |
| Workflow | AgentWorkflow/AgentStep/AgentValidationResult exist | Missing scheduling/approval/revision states and candidate version linkage |
| React | Tasks/schedules/decisions tabs and confirm actions | Sends fixed comments and null workflow ID; no full workflow review screen |
| Reservations | Member 3 service checks stock and saves reservations | Starts/commits its own transaction; must join final transaction safely |
| Performance | k6 health/dashboard baseline | No integrated agent latency or concurrent reservation evidence |

These are source findings, not executed vulnerability demonstrations or test results. Existing code in this snapshot cannot be attributed to a particular person without Git evidence.

## 3. Recommended approach and teammate handoffs

Use one shared PostgreSQL database and one backend-owned final transaction. Keep the AI service proposal-only. Reuse the current .NET 8/React architecture. Use a complete documented local deployment for the first evaluator run; hosted deployment is optional if the team has a practical approved host.

Before AI integration, obtain from the team:

- Prompts 01-04 or their agreed persisted contracts and shared AI-service implementation. The team Git URL, `main` base, and Member 4 branch are now established.
- Member 1: persisted crop-plan/planning output shape, crop-reference IDs/version/freshness policy, shared workflow versioning and coordinator rerun API.
- Member 2: field-analysis shape, freshness and missing-data/error semantics; shared FastAPI/provider/graph implementation and Dockerfile from its owner.
- Member 3: weather/resource output shape, units/currency, authoritative cost inputs, reservation availability query and transaction-enlistment contract.
- Flutter owner: confirm the now-present mobile source's auth/state-management conventions and workflow status endpoint expectations.

Do not infer exact shared wire schemas from this foundation. Confirm them in the team checkout before coding cross-member contracts. Local test fixtures can exercise agreed contracts, but must be labelled fixtures and must not be presented as real integrated agent execution.

Proposed defaults for team review: UTC instants with local display; at most three human-requested revision cycles; provider retries reuse the shared provider's existing bounded policy. Keep task conflict rules explicit: FarmTask currently has only DueAt, so genuine interval overlap requires an agreed start/end or duration extension. Until that contract exists, do not claim that matching deadlines is a complete scheduling conflict check.

## 4. Planned implementation milestones

### M0 - Plan and contribution baseline

Save this scope/gap plan. Obtain the Git destination and inspect the team's actual current branch before porting changes. Record dependencies as a handoff checklist. First proposed commit: `docs(member4): define approval integration scope and dependencies`.

### M1 - Complete manual Task/Approval behavior

Add missing task/schedule detail, update and soft-delete operations; keep approval records immutable. Add explicit status/farm/field/assignee/date filters as applicable, allow-listed sort fields, stable ID tie-breaking and pagination. Keep current route compatibility. Deleted approved work should use an auditable cancellation operation rather than erase the record.

Reject direct create/update attempts to mark work approved, rejected or revision-requested. Keep final decisions behind approver services. Validate active assignees, farm/field relationships, defined status values and dates; require nonblank reject/revision comments. Scope reads and history to authorized objects. Capture before/after status, actor, timestamp and reason for transitions. Test actual role and owner boundaries.

Exit: manual CRUD and decisions work through API/UI with validation and role tests. No shared AI dependency is required for this milestone.

### M2 - Agree and extend shared contracts

Reuse AgentWorkflow, AgentStep, AgentToolExecution, AgentValidationResult and ApprovalDecision. Preserve existing enum numbers when adding workflow states. Add candidate revision identity, concurrency/version checks, bounded revision count and generated-output linkage. Do not create a second ValidationResult table merely because the brief abbreviates its name.

Proposed backend API additions, to reconcile with the team's existing workflow routes before implementation:

- `GET /api/task-approval/workflows`: pending queue with filters and pagination.
- `GET /api/task-approval/workflows/{id}`: authorized aggregate review/status view.
- `GET /api/task-approval/workflows/{id}/history`: authorized transition/decision history.
- `POST /api/task-approval/workflows/{id}/approve`.
- `POST /api/task-approval/workflows/{id}/reject`.
- `POST /api/task-approval/workflows/{id}/request-revision`.

Decision requests identify the candidate revision and expected workflow version; approve supports a persisted idempotency key. Return 409 for stale/conflicting decisions and the existing successful result for a matching replay. Reject/revision require an officer-written reason. Do not allow existing item-level endpoints to bypass the integrated workflow gate for workflow-generated records.

### M3 - Implement Agent 4 in the existing AI service

Add `agents/scheduling_validation_agent.py`, `schemas/scheduling_validation.py`, and `tools/scheduling_tools.py` under the team's existing `ai-service`. Connect the existing Scheduling graph node. Load completed persisted upstream results for the same workflow and compatible revision; reject stale/mismatched results.

Allow only GetCurrentWorkflowContext, GetExistingFarmTasks, GetExistingIrrigationSchedules, GetResourceAvailability, GetExistingReservations, ValidateDateWindow and ValidateBudgetConstraint. Backend tool handlers enforce workflow scope and service authentication. No approve, unrestricted reserve, arbitrary SQL or final-task write tool.

Preserve workflowId, status, requiresHumanReview and warnings. Add requiresHumanApproval plus candidateTasks, candidateIrrigation, estimatedCost and constraints. CandidateReady always requires human approval. Missing upstream data returns MissingDependency with requiresHumanReview=true and no scheduling execution. Unknown costs remain null with warnings; they are not fabricated as zero.

### M4 - Validate and persist candidate evidence

Deterministically validate schema/size, identifiers/ownership, workflow and upstream revision, action allow-list, dates, task and irrigation conflicts, current stock/reservations, nonnegative availability, authoritative budget inputs and crop-reference availability/version. The backend computes eligibility and does not trust an LLM's valid flag. Persist validation linked to the candidate revision. Only a valid candidate becomes PendingOfficerApproval; unresolved blocking data remains outside the approval queue.

### M5 - Human decision and atomic execution

Semantic flow: upstream completion -> CandidateReady -> deterministic validation -> PendingOfficerApproval -> Completed on successful approved execution, Rejected on rejection, or RevisionRequested. Map these semantics to the shared team's states without duplicating its state machine. MissingDependency and validation failures never execute final work.

Approve locks/checks the workflow/candidate revision and rechecks mutable inputs inside a short transaction. Serialize conflicting reservations/schedules using consistent locking or equivalent tested database constraints. Coordinate with Member 3 so its service uses the same scoped DbContext and joins an existing transaction without committing or disposing the caller's transaction. Verify all stock-mutating paths participate in concurrency protection; the current ReserveAsync path does not visibly advance its application-managed RowVersion.

Atomically persist the approval decision, final tasks and irrigation, reservations via Member 3, crop-plan status/history, workflow completion and audit/notification record. Keep LLM calls and external notification delivery outside the transaction; use a persisted outbox if external delivery is needed. On failure roll back the business changes, then record a sanitized failure using a clean context without overwriting a concurrent successful decision. A recoverable conflict requires fresh validation before approval retry.

Revision records the reason and invalidates the earliest affected upstream result plus its downstream results; schedule-only revisions rerun Scheduling. Enforce the proposed three-cycle limit server-side. Rejection and revision create no final tasks/reservations. Cover concurrent approve-versus-reject/revise as well as approve-versus-approve.

### M6 - Review UI and farmer status

Extend the existing page with a pending queue and one WorkflowReview/ApprovalDetails route. Show farmer objective/plan, crop-reference provenance, all four step summaries, candidate schedule, cost/constraints, validation, warnings, timings, tool summaries and sanitized errors. Actions depend on role and server state, and display stale/revalidation errors. Collect real reject/revision text. Preserve React auth/context and existing UI components.

Using the team's Flutter source, add owner-scoped final workflow/approval status, tasks, irrigation, warnings and history with loading/error states. Reuse existing notification/status refresh patterns. Do not build a duplicate Flutter app in this snapshot.

### M7 - Integrated evidence and submission preparation

Run the exact Flutter -> ASP.NET -> PostgreSQL -> four agents -> backend validation -> React officer -> atomic commit -> Flutter status scenario. Document environment, versions and actual results. Extend performance coverage with modest separate read and reservation scenarios, database observations, workflow latency and timeout behavior. Record p50/p95, counts and success/failure rates actually measured; never fill in assumed numbers.

Update `docs/testing/performance-report.md`, `docs/database/er-diagram.md`, ADRs, README/SETUP, endpoint/schema/tool inventories, environment reference and evaluator startup instructions. Preserve AI_SERVICE_TOKEN, AI_PROVIDER, AI_MODEL, provider-key names, BACKEND_TOOL_BASE_URL, BACKEND_TOOL_TOKEN and ASP.NET AI__ServiceUrl/ServiceToken/ToolToken as agreed in Prompts 1/2. Confirm their exact spelling from those prompts. Document database setup, AI/backend reachability, web/mobile startup and real URLs/APK location. Do not claim an undeployed host or absent APK.

## 5. Test and acceptance matrix

- Manual API: CRUD/query validation, role matrix, wrong-farm access, farmer history isolation, invalid direct status transitions, required reasons and immutable decision history.
- Candidate: missing/stale dependencies, unavailable weather/reference, malformed JSON, unsupported actions, prompt injection treated as untrusted input, provider timeout and retry exhaustion.
- Validation: date windows/overlaps, invalid references, inconsistent versions, insufficient stock, current reservations and authoritative budget calculations.
- Decisions: valid approve/reject/revision, wrong role, stale candidate, repeated request, two simultaneous approvals, approve/reject race and maximum revision count.
- PostgreSQL: competing stock reservations, no negative availability, injected failure after each final-write stage, complete rollback, retry idempotency and no duplicate final records. EF InMemory tests cannot establish these guarantees.
- UI/mobile: officer review and real reasons, farmer own-record visibility, pending/rejected/revision/completed states and network failures.
- Full integration/performance: genuine provider and four-agent run distinguished from deterministic fixture tests; measured results and limitations preserved.

Use xUnit and Vitest for applicable tests, existing Python test tooling once present, browser checks and Flutter tests once source is integrated. No tests were run for this planning-only deliverable.

Transaction design references: [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions) explains atomic operations and shared transaction participation; [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) explains concurrency-token conflict detection. Validate the final implementation on PostgreSQL.

## 6. Git and contribution workflow

Current blocker: this directory has no .git metadata and no known remote. Do not initialize unrelated history, force-push a foundation snapshot, or attribute its existing code to Member 4. Ask for the team repository URL or clone path; then clone/open that repository, inspect its base and compare the relevant paths. Keep this snapshot intact.

Use the existing member branch if assigned; otherwise proposed branch is `member4/task-approval-integration` from the team's confirmed integration base. First push only this plan. Follow with real incremental changes corresponding to M1-M7, with tests in the commits that change behavior. Stage explicit paths and inspect the staged diff before each commit. Never stage .env, credentials, logs, build output or personal Codex skill files. Shared changes require clear descriptions of the integration reason and teammate review.

Suggested subsequent commit subjects: `fix(member4): enforce approval transitions and scoped history`; `feat(member4): complete task and schedule operations`; `feat(member4): validate scheduling candidates`; `feat(member4): commit approved workflows atomically`; `feat(member4): add workflow review and farmer status`; `test(member4): verify concurrency rollback and integration`; `docs(member4): document measured integration evidence`. Commit only what is actually complete; do not manufacture dates, authors, reviews or historical contributions.

PR descriptions should explain behavior, affected shared contracts, actual checks and remaining dependencies. Do not merge to the team's main branch as part of a request merely to push your contribution. Audit real branches/commits/PR evidence after Git access is established.

## 7. Human submission checklist

- [ ] Consolidated report PDF and architecture/design sections.
- [ ] Final ERD, API/Swagger evidence and ADRs.
- [ ] Actual tests, measured performance and deployment/local-run evidence.
- [ ] AI usage disclosure/logs based on actual work.
- [ ] Each member's real commits, tasks and PR/review evidence.
- [ ] Each member's own reflection and declaration.
- [ ] Demo video, actual live URLs if deployed, Flutter APK and permitted test accounts.

This checklist records outstanding evidence, not completed deliverables. This plan can guide independent manual-module work now; shared schema, scheduling conflict semantics and integration contracts must be resolved with the missing team base before full implementation.
