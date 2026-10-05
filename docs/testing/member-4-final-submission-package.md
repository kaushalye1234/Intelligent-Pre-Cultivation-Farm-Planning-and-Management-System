# Member 4 final submission package

This index collects the evidence for the Member 4 scheduling, approval, and farmer-visibility work after the Phase 3 merge into `dev`.

## Implementation evidence

- [Member 4 implementation plan](../plans/member-4-task-approval-integration-plan.md)
- [Scheduling and approval contract](../ai-usage/member-4-scheduling-approval-contract.md)
- [Phase 3 integration plan](../plans/member-4-phase3-final-integration.md)
- [Database ERD](../database/er-diagram.md)

## Verification evidence

- [Toolchain inventory](phase3-toolchain-inventory.md) distinguishes verified project/CI versions from unavailable exact environment versions.

- [Verification log](verification-log.md)
- [Performance report](performance-report.md)
- [Phase 3 final checklist](phase3-final-submission-checklist.md)
- [Member 4 test checklist](member-4-testing-checklist.md)
- [Group 04 consolidated report draft](../reports/group-04-consolidated-report-draft.md)

Local generated artifacts for the group leader (excluded from this Git contribution): `output/apk/agriassist-member4-dev-46c50b4-debug.apk`, `output/pdf/SE3090_G04_consolidated_report_DRAFT.pdf`, and `output/screenshots/member4-apk-login-2026-10-01.png`. The PDF is a review copy and must be completed with student-authored sections and deployment/video evidence before submission.

## Verified results

- Backend suite: 197 passed, 0 failed, 0 skipped against disposable PostgreSQL on the Member 4 PR #59 head.
- AI service suite: 86 passed.
- React suite: 94 passed; build and lint completed successfully.
- Flutter suite: 42 passed; analysis completed successfully.
- API smoke: health HTTP 200, authenticated Member 4 reads succeeded, and unauthenticated workflow access returned HTTP 401.
- Approval concurrency: one HTTP 200 and one HTTP 409 conflict, with one final approved task, schedule, and decision.
- CI for [PR #59](https://github.com/kaushalye1234/Intelligent-Pre-Cultivation-Farm-Planning-and-Management-System/pull/59) passed for backend, AI service, React, and Flutter before merge into `dev` (`46c50b4`).
- Android debug APK built in 286.8 seconds from that merged `dev` commit, signed with the debug key, installed, and launched on an Android emulator. Its SHA-256 is recorded in the [verification log](verification-log.md).
- On 2026-10-05, the public Render React page, API health and Swagger endpoints, and AI-service health endpoint each returned HTTP 200. These checks confirm reachability only; they are not a live authenticated workflow test.

## Known limitations to state during the demonstration

- The workflow timing data is a local deterministic-fallback fixture and is not a production performance claim.
- Earlier Windows APK attempts stalled; the 2026-10-01 build and emulator launch passed. Only the login screen was checked on the emulator, without a live backend sign-in.
- No external LLM-provider run is claimed without a configured provider key.
- Group 04 has not supplied a demonstration video or the other members' personal report sections. The Supabase-preserving deployment update merged in PR #71; Member 4 retrieval hardening remains in open PR #72. The linked report is a draft for assembly, not a final submitted group report.
