# Member 4 - Task/Approval implementation

This branch contains the existing Member 4 Task/Approval component plus the completed Milestone 1 manual workflow hardening. It is a source handoff for the four-member integration and is not a standalone runnable application while the shared foundation is absent from the remote repository.

The branch is based on the team's existing initial commit. Existing snapshot files are imported as one honest contribution; prior authorship and development history are not reconstructed.

## Included

- TaskApproval controller, service/interface, DTOs and validators.
- FarmTask, IrrigationSchedule and ApprovalDecision models.
- React TaskApprovalPage with task/schedule lists, create/edit actions, explicit submission and cancellation, and officer-entered approval comments.
- Focused backend business-rule tests and a frontend decision-dialog test.
- [Member 4 implementation and integration plan](docs/plans/member-4-task-approval-integration-plan.md).

The backend now supports filtered and paged task/schedule/history reads, detail endpoints, create/update/submit/cancel/soft-delete flows, approval decisions, ownership-scoped farmer history, deterministic task/schedule conflict checks, and audit metadata updates. Final decision endpoints restrict access to AgriculturalOfficer/Admin. Direct client-supplied status transitions are rejected, and reject/revision/cancellation actions require a reason.

## Required team foundation

The remote main branch contained only LICENSE when this handoff was prepared. These component files require the shared application to be merged before they can compile or run:

- ASP.NET Core 8 project/package files, Program.cs registration, AppDbContext/mappings/migrations, shared DTOs, validators, error/current-user services, auth and CropPlanning models.
- React project/package files, API client, auth context, routing, shared types, formatting/labels and UI components imported by TaskApprovalPage.
- Shared workflow models and database schema. Register `ITaskApprovalService -> TaskApprovalService` and all TaskApproval request validators in the shared backend composition root.
- Shared test projects and fixtures. The local snapshot's `AuditRegressionTests` includes a sequential repeated-task-approval test, but also contains Member 3 coverage; that mixed file is not imported as a Member 4-only file.

When the shared foundation is merged, replay these integration edits from the verified local workspace rather than replacing shared files wholesale:

- Register `IRequestValidator<CancellationRequest>, CancellationRequestValidator` in `Program.cs`.
- Add `ApprovalDecisionType.Cancelled = 4` to the shared frontend decision labels.
- Let the shared API client extract messages from the backend `{ error: { message } }` response shape.
- Pass `CancellationRequestValidator` to the TaskApproval service constructor in the existing mixed `AuditRegressionTests` fixture.

Merge these files into their existing paths when the team foundation is available. Do not replace shared application setup with a second implementation. This upload deliberately does not include other members' modules, environment files, personal Codex skills, local logs or build artifacts.

## Remaining integration work

Milestone 1 covers the manual Task/Approval lifecycle. The scheduling agent, deterministic candidate validation, workflow-level approval transaction, resource-reservation coordination, concurrency enforcement at the PostgreSQL boundary, and final workflow review UI remain for later milestones.

The assignment mentions Flutter integration, but `mobile/flutter_app` is absent from the verified checkout. That work remains unavailable until the team supplies the mobile project. See the plan for the complete milestone sequence and verification limits.

## Verification for Milestone 1

All 10 Member 4 source, test, and plan files were byte-compared with the verified local originals before staging.

- Backend test-project build: passed with 0 warnings and 0 errors.
- Focused TaskApproval/Audit tests: 7 passed.
- Full backend suite: 10 passed and 1 unrelated Auth integration test was blocked by Windows Event Log access in the sandbox.
- Frontend lint: passed with warnings and no errors.
- Frontend production build: passed.
- Frontend tests: 12 passed across 2 files.

The upload diff is checked for whitespace errors and accidental credential patterns before commit. The remote branch cannot be compiled by itself until the shared application foundation is merged; the successful checks above were run in the complete local workspace.

The plan's no-Git finding describes the original local snapshot at inspection time. This handoff branch now preserves the actual team Git history; it does not retroactively give that snapshot an authorship history.
