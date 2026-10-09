# Verification Log

## Phase 1 - Backend Skeleton

Verified with:

- `dotnet restore`
- `dotnet build`
- API started locally
- `/health` returned `Healthy`

## Phase 2 - Auth/User Management

Verified with the Testing environment and in-memory database:

- Admin login returned a JWT
- `/api/auth/profile` returned the authenticated admin profile
- `/api/users` returned the seeded users

## Phase 3 - CropPlanning

Verified with `scripts/test-phase3-crop-planning.ps1`:

- Farmer created a farm and field
- Seeded crop types were listed
- Preliminary crop plan request returned status `3`
- Request history returned one entry
- Duplicate active request returned HTTP 409

## Phase 4 - Inspections

Verified with `scripts/test-phase4-inspections-v2.ps1`:

- Field officer created an inspection
- Field officer created an observation
- High severity issue escalated to status `2`
- Invalid image upload returned HTTP 400

Real Cloudinary upload was not verified because Cloudinary credentials were not configured.

## Phase 5 - Resources

Verified with `scripts/test-phase5-resources.ps1`:

- Resource officer created category, supplier, resource, and stock
- Available quantity calculated as expected
- Reservation succeeded
- Over-reservation returned HTTP 409
- Release succeeded
- Stock transaction history returned two entries

## Phase 6 - TaskApproval and AgentWorkflow Schema

Verified with `scripts/test-phase6-task-approval.ps1`:

- Field/agricultural officer logins succeeded
- Farm task was created pending approval
- Farm task approval succeeded
- Irrigation schedule was created pending approval
- Schedule approval succeeded
- Approval list returned decisions

No AI execution was performed.

## Phase 7 - Dashboard, Tests, Migrations

Verified with:

- `dotnet build`
- `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj` with 4 passing tests
- `dotnet ef database update` against Supabase
- Local API `/health` returned `Healthy`
- Supabase-backed admin login/profile/users query succeeded

## Phase 8 - React Staff/Admin Console

Verified with:

- `npm run build` in `frontend/react-app`
- `npm test` in `frontend/react-app` with 4 passing tests

The tests cover login form validation, protected route redirect, empty table state, and API error normalization.

## Phase 9 - Flutter Farmer App

Verified with:

- `flutter analyze` with no issues
- `flutter test` with 2 passing widget tests

An Android debug APK build was attempted with `flutter build apk --debug`, but the Gradle build stayed running silently after a plugin SDK warning and was stopped. APK compile is not claimed as verified.

## Member 4 Phase 2 integration verification

Verified on 2026-09-17 with:

- All five EF Core migrations applied to an isolated PostgreSQL 16 container; 29 public tables created.
- Backend xUnit suite: 44 passing tests.
- AI service Docker image (Python 3.12): 27 passing tests.
- AI service `/health`: HTTP 200; unauthenticated workflow request: HTTP 401; authenticated malformed request reached schema validation with HTTP 422.
- ASP.NET `/health`: HTTP 200 on port 5087.
- React development server: HTTP 200 on port 5173.
- Flutter farmer status implementation is present; local SDK verification is recorded below.

PostgreSQL competing-request approval, rollback, and no-duplicate final-record behavior still require dedicated integration scenarios beyond migration application and EF InMemory service tests.

The repeatable `scripts/test-member4-postgres.ps1` smoke check passed against the disposable PostgreSQL container: five migrations and five required Member 4 tables were found, and rollback-to-savepoint and row-lock probes passed. API-level competing approval tests remain pending.

Flutter follow-up on 2026-09-17: Flutter 3.47.4 was detected, `flutter pub get` succeeded, `flutter analyze --no-pub` reported no issues, and the login, crop-planning, and inspection test files each passed when run separately (6 tests total). The combined test command stalled during multi-file loading and the debug APK Gradle task did not complete in this environment; no APK build is claimed.

API smoke follow-up on 2026-09-19: ASP.NET `/health` and AI `/health` returned HTTP 200; AgriculturalOfficer login succeeded; workflow, task, schedule, and approval reads returned successfully with zero records in the disposable database; unauthenticated workflow access returned HTTP 401. Candidate generation and API concurrency require a real upstream-complete workflow fixture.

Member 4 live approval verification on 2026-09-19: the local Docker AI service reached the host API after setting its ignored development-only `BACKEND_TOOL_BASE_URL` to `http://host.docker.internal:5087`. A disposable workflow completed coordinator, field-analysis, weather/resource, and scheduling steps; candidate revision 1 was generated for workflow `d807e840-1c3a-4760-9055-6972fc429b80`. Two simultaneous approval requests produced exactly one HTTP 200 and one HTTP 409, and the final read showed one approved task, one approved irrigation schedule, and one approval decision. The coordinator used its deterministic fallback because no LLM provider key was configured; weather and inspection inputs remained safe-review warnings, as expected for the local fixture.

Farmer visibility follow-up on 2026-09-19: the owning farmer could read the completed workflow and its one decision, and farmer-scoped task and schedule reads returned one approved task and one approved irrigation schedule. The global approval queue returned zero farmer rows because it is intentionally officer-scoped; the workflow review response is the farmer-visible approval-history surface.

Phase 3 release-check follow-up on 2026-09-19: backend xUnit passed 44 tests; React production build and lint completed successfully (lint retained existing non-blocking warnings). A ten-request local API health sample measured 1.03-266.52 ms with a 28.91 ms average. The global Python environment did not have pytest, while the previously verified Docker AI image had 27 passing pytest tests. The full React Vitest and Flutter test runners stalled during this run and were stopped; no new pass claim is made for those combined commands.

Limitation remediation follow-up on 2026-09-19: the compatible AI Docker runtime executed all 27 pytest tests successfully. React Vitest completed all 18 tests when allowed to finish (about 35 seconds locally). The Flutter CI job now runs each widget test file separately to avoid the aggregate-runner hang; this Windows session still did not complete the Flutter runner or Android Gradle APK build, so no APK artifact is claimed.

## Phase 10 - Member 2 Inspections AI

Verified on 2026-09-14 with:

- `python -m py_compile agents\crop_field_analysis_agent.py schemas\field_analysis.py tools\inspection_tools.py graph\workflow_graph.py main.py` in `ai-service`
- `.venv\Scripts\python.exe -m pytest -q` in `ai-service` with 18 passing tests
- `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj` with 22 passing tests
- `npm run lint` in `frontend/react-app` exited 0 with non-blocking oxlint warnings for page-loader/AuthContext patterns
- `npm test` in `frontend/react-app` with 15 passing tests
- `npm run build` in `frontend/react-app`
- `flutter analyze` in `mobile/flutter_app` with no issues
- `flutter test` in `mobile/flutter_app` with 6 passing tests

The AI-service test run used a local `ai-service/.venv` created from `requirements.txt`. Cloudinary image upload is implemented through ASP.NET and covered with fake success/failure tests; real Cloudinary credentials were not used.

## Member 4 evidence-linked scheduling, 2026-09-30

Verified on `member4/explainable-scheduling` using a disposable PostgreSQL 16 container on loopback port 55433 and database `agriassist_member4_test`:

- `dotnet restore` and Release `dotnet build --no-restore`: exit 0, no build warnings or errors.
- Full Release backend xUnit suite with `AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING` set to that disposable database: **195 passed, 0 skipped, 0 failed**. This includes one-winner competing approvals, forced reservation-insert rollback, exact persistence of a three-decimal approved quantity, and safe rejection of a malformed persisted stock ID.
- EF migration `Member4ResourceQuantityPrecision` applied successfully. The Member 4 PostgreSQL smoke script found 11 migrations and five required tables; rollback-to-savepoint and row-lock probes passed.
- Python 3.12 source compile: exit 0. Full AI-service pytest suite: **86 passed**, with one dependency deprecation warning. Two HTTP ASGI-route cases exercised authenticated FastAPI requests for ready and blocked scheduling outputs, including source links and no reservation under high weather risk. The graph and agent use deterministic fixtures; no live LLM or weather-provider response is claimed.
- `npm ci`, React lint, production build, and full Vitest suite: exit 0; **93 passed**. Lint retained existing non-blocking React hook/Fast Refresh warnings, and Vite reported a chunk-size warning.
- `flutter pub get`, `flutter analyze`, and the full Flutter suite: exit 0; analysis found no issues and **42 tests passed**.
- `git diff --check`: exit 0. No source conflict markers were found by the repository scan.

The version-2 tests cover a ready proposal with zero irrigation rules, blocked review, deactivated-profile approval rejection, source/reason rendering, and farmer blocked messaging. The FastAPI HTTP route is also exercised in-process.

Live follow-up on 2026-10-01: the ASP.NET API on loopback port 5097 called the Python AI service on port 8007 against two seeded workflows in the disposable PostgreSQL database. A verified-profile, Medium-weather fixture returned HTTP 200, `CandidateReady`, workflow status 8 (`PendingOfficerApproval`), one sourced task, zero irrigation entries, one sourced reservation, and a valid ASP.NET validation result. A separate High-weather fixture returned HTTP 200, `CandidateBlocked`, workflow status 12, one reviewable task, zero reservations, `requiresHumanApproval=false`, and an invalid approval validation result. PostgreSQL queries found zero final tasks, irrigation schedules, and reservations for both workflows after generation. No approval was issued during this live smoke run; the isolated PostgreSQL xUnit tests separately verified transactional approval, concurrency, and rollback. The upstream outputs were seeded deterministic fixtures, not live weather or LLM responses. React officer review and Flutter blocked status passed component tests, but a combined browser session was not run.

GitHub draft PR #59 at commit `522bcc8` passed the backend, AI-service, React, and Flutter jobs.

Flutter CI follow-up on 2026-10-01: after the fixed test fixture date became overdue, `responsive_journey_test.dart` exposed a 29-pixel right overflow in the task due-date row on a 360-pixel phone. The row now constrains and wraps its date text. The focused responsive test passed, `flutter analyze` found no issues, and the full local Flutter suite passed 42 tests. The initial Flutter job and its unchanged-head rerun failed before this layout fix; the backend, AI-service, React, and Flutter CI jobs subsequently passed on commit `c0d4677`.

Officer-browser follow-up on 2026-10-01: React on loopback port 5173, ASP.NET on 5098, Python AI on 8008, and disposable PostgreSQL 16 on 55434 were used together. The browser authenticated as a disposable Agricultural Officer and loaded the real queue and review routes. Workflow `713272ff-0ec8-4486-a315-99131d3473f0` displayed `Pending Officer Approval`, one sourced crop-stage task, zero irrigation entries because no verified rule existed, one sourced resource reservation, and officer decision controls. Workflow `5b6d8ebd-1b42-4cc4-b0fe-8ab77b76e954` displayed `Candidate Blocked`, the High-weather reason, one reviewable sourced task, zero reservations, and no Approve control. The queue showed both officer-facing labels after a regression fix for the internal `HumanApproval` label. Browser console errors and warnings were zero, and queue/API requests loaded successfully. PostgreSQL queries found zero final tasks, irrigation schedules, and reservations for these workflows. No browser approval was submitted; the approval transaction remains evidenced by the separate PostgreSQL xUnit tests. The upstream data was synthetic persisted Member 1-3 evidence, not a live weather or LLM response. The synthetic fixture had no linked pre-planting assessment, so that panel showed `Not started`. After the queue fix, React lint and production build exited 0 and Vitest passed **94 tests**; lint retained existing non-blocking warnings and Vite retained a chunk-size warning. The PR #59 checks track remote CI for each pushed commit.

Code-review follow-up on 2026-10-01: a new backend regression first demonstrated that a fabricated stage reason could enter the officer queue despite a valid source ID. ASP.NET now checks preparation, stage, irrigation, and reservation explanations against the persisted Member 1-3 and verified-profile evidence before a candidate can be considered ready. AI scheduling now retains otherwise valid reservation proposals when a candidate is blocked by high or invalid weather or a scheduling-window issue; insufficient, duplicate, or ambiguous resource evidence still produces no reservation. If later crop stages cannot fit the selected window, the blocked output lists each unscheduled stage ID. Focused tests passed, including 14 AI scheduler/API cases and the fabricated-explanation regression. The full AI suite passed **86 tests**. The Release backend suite passed **197 tests with 0 skipped** using disposable PostgreSQL 16 on loopback port 55435; PostgreSQL approval concurrency, rollback, and three-decimal persistence tests ran. A follow-up regression confirms that a verified irrigation rule key of 120 characters is accepted when its explanation is truncated to the AI contract's 100-character limit; backend validation now applies the same limit. `git diff --check` passed. The older browser walkthrough above predates these final scheduler and validation changes; the current behavior is covered by the updated tests and PR CI.

Submission artifact follow-up on 2026-10-01: from clean `origin/dev` merge commit `46c50b4` in an isolated worktree, `flutter doctor -v` reported no issues (Flutter 3.47.4, Android SDK 37, Android Studio JDK 21). `flutter pub get` exited 0 and updated only the isolated worktree's lockfile and analysis options; those incidental edits were not included in the source contribution. `flutter build apk --debug --no-pub` exited 0 after **286.8 seconds** and created `app-debug.apk` (159,443,118 bytes). `aapt dump badging` reported package `com.agriassist.mobile`, version `1.0.0`, min SDK 24, target SDK 36. `apksigner verify --verbose` succeeded with one signer using APK Signature Scheme v2. SHA-256: `82E8AA8ADF533F9F901C8F988BBA2B66FDE95B7ABBEB5D88025B668E387E44C0`. The APK was copied locally to `output/apk/agriassist-member4-dev-46c50b4-debug.apk` and was not staged for Git. `adb install -r` returned `Success` on `Medium_Phone_API_36.1`; the app's main activity stayed resumed and the login screen was visually inspected in `output/screenshots/member4-apk-login-2026-10-01.png`. An Android System UI cold-boot dialog appeared initially and was dismissed before the clean screenshot. No sign-in or backend-connected mobile flow was exercised in this APK smoke check.

Documentation follow-up on 2026-10-01: a disposable `ASPNETCORE_ENVIRONMENT=Testing` API with EF InMemory returned `/health` HTTP 200 and `/swagger/v1/swagger.json` HTTP 200 on loopback port 5099. The [OpenAPI v1 snapshot](../api/agriassist-openapi-v1.json) contains 22 `/api/task-approval/*` paths and the current API description. This Swagger run does not prove PostgreSQL or deployment readiness. PR #59 had already passed backend, AI, React, and Flutter CI and merged into `dev`; the report draft distinguishes these automated checks from the local artifact build.

Report follow-up on 2026-10-01: `docs/reports/group-04-consolidated-report-draft.md` was rendered to the local six-page `output/pdf/SE3090_G04_consolidated_report_DRAFT.pdf` with ReportLab. Every page was rendered to PNG and visually inspected; the architecture and workflow diagrams, Member 4 section, page numbers, and emulator login screenshot are legible. The source and PDF explicitly identify missing group-authored sections, deployment URLs, video, and signatures. No finished consolidated submission or personal reflection is claimed.

## Member 4 optional profile retrieval and Render preparation, 2026-10-05

Verified from a clean worktree on `member4/render-phase4-clean`, rebased onto `origin/dev` at `bb3bd9dc778a1e341ed05e0e141f0cf1fad3941a`:

- `dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release`: **270 passed, 15 skipped, 0 failed**. The skipped cases require PostgreSQL; this run does not verify PostgreSQL migration, concurrency, JSONB, or transactional behavior.
- React `npm run lint`, `npm run build`, and `npm test`: all exited 0; **109 tests passed across 16 files**. Lint reports existing React hook/Fast Refresh warnings and Vite reports the existing large-chunk warning.
- AI `python -m compileall -q agents graph providers schemas tools tests main.py` and `python -m pytest`: **186 passed, 0 failed**. This machine has Python 3.14.3 while the repository locks Pydantic 2.10.4, whose native core does not support Python 3.14. For this local run only, an isolated test environment used Pydantic 2.12.4; the committed requirements were not changed. The result is not equivalent to the Python 3.12 CI dependency set and should be confirmed by CI before merge. The run emitted dependency and Pydantic warnings.
- No Flutter files changed in this follow-up, and Flutter was not run. Render YAML parsed locally; no Render account resources, hosted URLs, live provider calls, or hosted end-to-end checks were created.
- The optional one-tool profile retrieval remains disabled by default. The user’s local AI-service `.env` was preserved in the main checkout; it does not define `SCHEDULING_PROFILE_RETRIEVAL_ENABLED`, so the code default applies.


GitHub follow-up on 2026-10-05: draft PR #67 ran the backend, AI-service, React, and Flutter GitHub Actions jobs on commit `b5a6a29`; all four passed. A later report-only commit updates the handoff status and does not change application code.
