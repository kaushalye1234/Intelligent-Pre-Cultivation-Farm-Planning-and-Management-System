# Phase 3 final submission checklist

## Verified

- [x] Backend build and 44 xUnit tests
- [x] AI service Docker health and 27 pytest tests from prior verified image run
- [x] React production build and lint
- [x] Real four-step workflow and candidate generation
- [x] Concurrent approval: one HTTP 200 and one HTTP 409
- [x] Farmer-scoped task, schedule, workflow, and decision visibility
- [x] PostgreSQL migration, rollback, and row-lock smoke evidence
- [x] API health latency sample recorded
- [x] Flutter SDK, dependency, analysis, and individual test evidence recorded

## Limitations to state in the submission

- The local workflow used deterministic AI fallback because no LLM provider key was configured.
- Weather, inspection, and inventory warnings remained human-review inputs in the disposable fixture.
- The combined React/Flutter test runners stalled in this Windows environment during the final pass.
- The Flutter debug APK Gradle task did not complete; no APK artifact is claimed.
- No production deployment or load-test percentile claim is made.

## Files to attach or link

- `docs/testing/verification-log.md`
- `docs/testing/performance-report.md`
- `docs/database/er-diagram.md`
- `docs/plans/member-4-phase3-final-integration.md`
- `.github/workflows/ci.yml`
- Swagger export or screenshots from the local API
