# Repository Guidelines

## Project Structure & Module Organization

AgriAssist is a multi-client farm operations foundation. The ASP.NET Core 8 API lives in `backend/AgriAssist.Api`, with xUnit tests in `backend/AgriAssist.Api.Tests`. The staff/admin web console is `frontend/react-app`, with source in `src`, assets in `src/assets` and `public`, and page-level tests beside React pages. The Flutter farmer app is in `mobile/flutter_app`, with app code under `lib` and tests under `test`. Docs, ADRs, plans, database notes, and verification logs are in `docs`; k6 baselines are in `performance`; automation is in `scripts`.

## Build, Test, and Development Commands

- `dotnet build backend\AgriAssist.Api\AgriAssist.Api.csproj`: compile the API.
- `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj`: run backend xUnit tests.
- `dotnet run --project backend\AgriAssist.Api\AgriAssist.Api.csproj`: start the API locally.
- `cd frontend\react-app; npm ci; npm run dev`: install and run the Vite web app.
- `npm run build`, `npm run lint`, `npm test`: type-check/build, lint with oxlint, and run Vitest.
- `cd mobile\flutter_app; flutter pub get; flutter analyze; flutter test`: prepare, analyze, and test Flutter.
- `k6 run -e API_BASE_URL=http://localhost:5000 performance\k6-basic.js`: run the optional performance baseline.

## Coding Style & Naming Conventions

Use existing feature folders as boundaries: controllers, services, validators, DTOs, and models are grouped by domain such as `CropPlanning`, `Inspections`, `Resources`, and `TaskApproval`. Keep backend business rules in services, not controllers. Use C# `PascalCase` for public types and members, `camelCase` for locals, and `Async` suffixes for asynchronous methods. React components use `PascalCase`, tests use `*.test.tsx`, and shared UI belongs in `src/components`. Flutter files use Dart `lower_snake_case`; keep app state in Provider.

## Testing Guidelines

Add or update tests for changes to auth, authorization, validation, persistence, and workflow transitions. Backend tests use xUnit `[Fact]` methods with behavior-style names, for example `Reject_requires_an_officer_written_comment`. React tests use Vitest and Testing Library. Flutter tests use `flutter_test` and `*_test.dart` names.

## Commit & Pull Request Guidelines

Recent history uses merge commits plus short fix summaries. Prefer concise, imperative commit subjects with scope when helpful, such as `fix: validate irrigation overlap`. PRs should describe backend, frontend, mobile, database, and configuration impact; link issues or plans; include screenshots for UI changes; and list the exact verification commands run.

## Security & Configuration Tips

Do not commit `.env` files or secrets. Start from `.env.example` files and configure production secrets in the hosting platform. Keep AgenticAI execution disabled.
