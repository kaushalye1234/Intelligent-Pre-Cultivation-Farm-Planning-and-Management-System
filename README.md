# AgriAssist AI - Basic Foundation

AgriAssist is an ASP.NET Core, React, and Flutter foundation for farm operations. This repository contains:

- ASP.NET Core 8 Web API backend with EF Core 8 and PostgreSQL/Supabase support
- React + Vite staff/admin console
- Flutter + Provider farmer mobile app
- Shared AgentWorkflow schema and disabled AgenticAI client placeholder
- Cloudinary integration path for inspection images

No LLM or agent execution is enabled in this foundation. The AI-facing schema and interfaces exist so later prompts can build on them.

## Project Structure

```text
backend/AgriAssist.Api/          ASP.NET Core API
backend/AgriAssist.Api.Tests/    xUnit backend tests
frontend/react-app/              React + Vite web console
mobile/flutter_app/              Flutter farmer app
docs/                            ERD, ADRs, test notes, AI usage notes
performance/                     k6 baseline script
.github/workflows/               CI workflow YAML
```

## Member 4 Task/Approval Status

The `member4/task-approval-foundation` branch adds the completed first milestone of the Member 4 Task/Approval work:

- Filtered and paged task, irrigation-schedule, and approval-history reads
- Detail, create, update, submit, cancel, and restricted soft-delete operations
- Server-owned workflow transitions and officer-only final decisions
- Required rejection, revision, and cancellation reasons
- Farmer-scoped approval history and deterministic conflict checks
- React create/edit/submit/cancel and officer decision flows
- Focused backend business-rule and frontend decision-dialog tests

See the [Member 4 implementation and integration plan](docs/plans/member-4-task-approval-integration-plan.md) for the implemented scheduling workflow and remaining integration evidence.

Phase 2 adds the fourth-agent scheduling proposal, deterministic candidate validation, revision/version-aware workflow review, and the officer approval gate. See the [Member 4 scheduling and approval contract](docs/ai-usage/member-4-scheduling-approval-contract.md) for endpoints, payloads, and verification limits.

## Local Commands

```powershell
dotnet build backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj

cd frontend\react-app
npm install
npm run build
npm test

cd ..\..\mobile\flutter_app
flutter pub get
flutter analyze
flutter test
```

## Runtime Notes

- Backend secrets live in ignored `.env` files.
- React reads `VITE_API_BASE_URL`.
- Flutter defaults to `http://10.0.2.2:5087/api` for Android emulator use and can be overridden with `--dart-define AGRIASSIST_API_BASE_URL=...`.
- Cloudinary uploads require backend Cloudinary environment values.
- The AgenticAI client uses `AI__ServiceUrl` and `AI__ServiceToken`; unavailable or invalid AI responses are persisted as safe failures and never create final work.

## Member 4 Verification

The Phase 2 implementation was verified on 2026-09-17:

- Backend Release build passed with 0 warnings and 0 errors; all 44 xUnit tests passed.
- The Python 3.12 AI-service image built and all 27 pytest tests passed.
- React lint completed with warnings and no errors, the production build passed, and all 18 Vitest tests passed.
- EF Core reports no pending model changes, and the PostgreSQL migration SQL was generated and inspected.
- All five EF Core migrations were applied successfully to an isolated PostgreSQL 16 container on port 55432; the resulting schema contains 29 public tables. This verifies migration application only; competing-request transaction tests remain separate.

The Milestone 1 changes were verified again after merging the shared foundation:

- Backend test-project build passed with 0 warnings and 0 errors.
- Seven focused TaskApproval and regression tests passed.
- The frontend production build passed.
- Twelve frontend tests passed across two files.
- Frontend lint completed with warnings and no errors.

The earlier full backend run passed 10 of 11 tests; the unrelated Auth integration test was blocked by Windows Event Log access in the sandbox. Flutter verification is unavailable because the Flutter SDK is not installed in this environment.
