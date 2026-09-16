# AgriAssist Agent Guide

This file applies to the whole repository. Use it when an AI coding agent plans, implements, reviews, or tests project changes.

## Project layout

- `backend/AgriAssist.Api`: ASP.NET Core 8 API, EF Core 8, and PostgreSQL integration.
- `backend/AgriAssist.Api.Tests`: xUnit service and integration tests.
- `ai-service`: FastAPI and LangGraph agents.
- `frontend/react-app`: React, TypeScript, and Vite web application.
- `mobile/flutter_app`: Flutter mobile application.
- `docs/ai-usage`: persisted contracts exchanged between member agents.
- `docs/plans`: implementation and integration plans.

Verify the active branch and current files before changing code. Never continue while tracked source contains Git conflict markers.

## Team ownership

1. Member 1 owns crop and season planning plus `CropPlanningCoordinatorAgent`.
2. Member 2 owns field inspections, crop issues, and `CropFieldAnalysisAgent`.
3. Member 3 owns resources, inventory, weather, and `WeatherResourceAgent`.
4. Member 4 owns farm tasks, irrigation schedules, approvals, `SchedulingValidationAgent`, and final workflow integration.

Shared infrastructure includes workflow models, `AppDbContext`, migrations, `Program.cs`, the ASP.NET AI client, the LangGraph workflow, API contracts, routing, and CI. Preserve every member's working path when editing shared files.

Member 4 must consume the persisted upstream contracts documented in:

- `docs/ai-usage/member-2-crop-planning-contract.md`
- `docs/ai-usage/member-3-weather-resource-contract.md`

Do not fabricate missing upstream outputs. Return a safe missing-dependency or human-review result instead.

## Implementation rules

- Keep authorization in controllers and ownership/business rules in services.
- Keep input validation in the existing validators.
- Preserve role values: Farmer `1`, FieldOfficer `2`, ResourceOfficer `3`, AgriculturalOfficer `4`, Admin `5`.
- Use typed DTOs at service boundaries and keep ASP.NET and Python JSON contracts aligned.
- Use UTC for persisted timestamps and convert only for display.
- Preserve audit fields, soft deletion, inventory concurrency, and transaction boundaries.
- Never create final tasks, irrigation schedules, or inventory reservations before explicit human approval.
- Recheck inventory and scheduling constraints inside the approval transaction because upstream analysis is a snapshot.
- Update EF mappings, migrations, and `AppDbContextModelSnapshot` together when the data model changes.
- Do not commit secrets, `.env` files, build output, virtual environments, `node_modules`, `__pycache__`, or `.pyc` files.

Use the installed `agriassist-feature`, `agriassist-data`, `agriassist-test`, and `agriassist-run` skills when they are available and relevant.

## Required verification

Run checks affected by the change before committing. For shared integration changes, run all available checks.

```powershell
dotnet restore backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj
dotnet build backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-restore
$env:Logging__EventLog__LogLevel__Default = 'None'
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-build
```

```powershell
cd ai-service
python -m pip install -r requirements.txt
python -m compileall -q .
python -m pytest
```

```powershell
cd frontend/react-app
npm ci
npm run lint
npm run build
npm test
```

When Flutter is installed:

```powershell
cd mobile/flutter_app
flutter pub get
flutter analyze
flutter test
```

Do not report PostgreSQL-specific migration, transaction, JSONB, or concurrency behavior as verified by EF InMemory tests. Use an isolated PostgreSQL database when those behaviors change.

## Git and CI workflow

- Branch from the latest clean `dev` for feature work.
- Use `member4/task-approval-phase2` for Member 4 Phase 2 after the integration repair reaches `dev`.
- Keep commits focused and do not mix another member's unrelated feature work into a Member 4 commit.
- Scan for conflict markers and run `git diff --check` before committing.
- Pull requests to `dev` or `main` must pass backend, AI-service, React, and Flutter jobs that apply to the changed project.
- Fix CI commands in the workflow when clean-checkout behavior differs from a prepared local machine.
- Do not push directly to `main`. Use a reviewed pull request.

## Phase 2 completion gate

Member 4 Phase 2 is ready for review only when the scheduling agent consumes compatible Member 1-3 outputs, produces deterministic candidate tasks and irrigation schedules, records warnings and validation results, requires human approval, and has tests for missing dependencies, stale revisions, conflicts, rejection, revision, rollback, and concurrent approval attempts.
