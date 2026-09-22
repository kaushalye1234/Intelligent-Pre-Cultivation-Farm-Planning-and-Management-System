# Member 4 final submission package

This index collects the evidence for the Member 4 scheduling, approval, and farmer-visibility work after the Phase 3 merge into `dev`.

## Implementation evidence

- [Member 4 implementation plan](../plans/member-4-task-approval-integration-plan.md)
- [Scheduling and approval contract](../ai-usage/member-4-scheduling-approval-contract.md)
- [Phase 3 integration plan](../plans/member-4-phase3-final-integration.md)
- [Database ERD](../database/er-diagram.md)

## Verification evidence

- [Verification log](verification-log.md)
- [Performance report](performance-report.md)
- [Phase 3 final checklist](phase3-final-submission-checklist.md)
- [Member 4 test checklist](member-4-testing-checklist.md)

## Verified results

- Backend suite: 80 passed, 0 failed, 0 skipped when the disposable PostgreSQL connection string was supplied.
- AI service Docker suite: 27 passed.
- React suite: 18 passed; build and lint completed successfully.
- API smoke: health HTTP 200, authenticated Member 4 reads succeeded, and unauthenticated workflow access returned HTTP 401.
- Approval concurrency: one HTTP 200 and one HTTP 409 conflict, with one final approved task, schedule, and decision.
- CI for the Phase 3 reliability PR passed for backend, AI service, React, and Flutter.

## Known limitations to state during the demonstration

- The workflow timing data is a local deterministic-fallback fixture and is not a production performance claim.
- The Flutter aggregate runner and local debug APK Gradle task did not complete in the Windows environment; CI runs Flutter test files separately.
- No external LLM-provider run is claimed without a configured provider key.
