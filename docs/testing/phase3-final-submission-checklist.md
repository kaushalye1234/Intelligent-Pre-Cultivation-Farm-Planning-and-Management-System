# Phase 3 final submission checklist

## Toolchain inventory

- [Version inventory](phase3-toolchain-inventory.md) records configured/pinned versions and clearly identifies exact host patch versions or image digests that were not captured.

## Verified

- [x] Backend Release suite: 197 xUnit tests against disposable PostgreSQL, 0 skipped
- [x] AI service: 86 pytest tests
- [x] React production build, lint, and 94 Vitest tests
- [x] Real four-step workflow and candidate generation
- [x] Concurrent approval: one HTTP 200 and one HTTP 409
- [x] Farmer-scoped task, schedule, workflow, and decision visibility
- [x] PostgreSQL migration, rollback, and row-lock smoke evidence
- [x] API health latency sample recorded
- [x] Flutter CI passed; 42 widget tests and analysis passed on PR #59
- [x] Android debug APK built from merged `dev` commit `46c50b4`, verified with `apksigner`, installed and launched on an Android emulator on 2026-10-01
- [x] Public Render endpoint reachability checked read-only on 2026-10-05: React root, API `/health`, API Swagger JSON, and AI `/health` returned HTTP 200. AI health required a retry after a first timeout.

## Limitations to state in the submission

- The local workflow used deterministic AI fallback because no LLM provider key was configured.
- Weather, inspection, and inventory warnings remained human-review inputs in the disposable fixture.
- The previous Windows Gradle attempts stalled; the 2026-10-01 clean-worktree build completed in 286.8 seconds. The debug APK uses the project's debug signing configuration and is not a production release package.
- Public React/API/Swagger/AI URLs respond, but these reachability checks alone do not prove Supabase database readiness, authenticated end-to-end workflow, or a live AI scheduling run. The Supabase-preserving Render/mobile update merged in PR #71; the Member 4 profile-retrieval hardening remains in open PR #72.
- A demonstration video has not been supplied or checked for access.
- The single consolidated group PDF still needs the other three members' own sections, AI logs and reflections, signed declarations, and a group AI declaration. Those must be supplied and reviewed by the students.
- No external LLM-provider run or load-test percentile claim is made.

## Files to attach or link

- `docs/testing/verification-log.md`
- `docs/testing/performance-report.md`
- `docs/database/er-diagram.md`
- `docs/plans/member-4-phase3-final-integration.md`
- `.github/workflows/ci.yml`
- [OpenAPI v1 export from the local Testing API](../api/agriassist-openapi-v1.json)
- Local debug APK: `output/apk/agriassist-member4-dev-46c50b4-debug.apk` (generated file, excluded from Git)
- Emulator launch screenshot: `output/screenshots/member4-apk-login-2026-10-01.png` (generated file, excluded from Git)
- [Group 04 report assembly draft](../reports/group-04-consolidated-report-draft.md)
- Local rendered review PDF: `output/pdf/SE3090_G04_consolidated_report_DRAFT.pdf` (generated file, excluded from Git)
