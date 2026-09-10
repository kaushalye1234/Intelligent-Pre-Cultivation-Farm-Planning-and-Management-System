$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$root = Join-Path $PSScriptRoot ".."

function Ensure-Dir($Path) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Write-NoBom($Path, $Content) {
    $dir = Split-Path $Path -Parent
    Ensure-Dir $dir
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

Write-NoBom (Join-Path $root "README.md") @'
# AgriAssist AI - Basic Foundation

AgriAssist is a BASIC, non-AI foundation for farm operations. This repository contains:

- ASP.NET Core 8 Web API backend with EF Core 8 and PostgreSQL/Supabase support
- React + Vite staff/admin console
- Flutter + Provider farmer mobile app
- Shared AgentWorkflow schema and disabled AgenticAI client placeholder
- Cloudinary integration path for inspection images

No LLM or agent execution is enabled in this prompt. The AI-facing schema and interfaces exist only so later prompts can build on them.

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
- Flutter defaults to `http://10.0.2.2:5000/api` for Android emulator use and can be overridden with `--dart-define AGRIASSIST_API_BASE_URL=...`.
- Cloudinary uploads require backend Cloudinary environment values.
'@

Write-NoBom (Join-Path $root "SETUP.md") @'
# AgriAssist Setup

## Prerequisites

- .NET 8 SDK
- Node.js and npm
- Flutter SDK
- PostgreSQL database, Supabase recommended
- Cloudinary account for inspection image storage
- k6 only if you want to run the performance baseline

## Backend

1. Copy `backend/AgriAssist.Api/.env.example` to `backend/AgriAssist.Api/.env`.
2. Fill in database, JWT, Cloudinary, weather, and disabled AI placeholder settings.
3. From the repository root, run:

```powershell
dotnet restore backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet build backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet ef database update --project backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet run --project backend\AgriAssist.Api\AgriAssist.Api.csproj
```

The API exposes Swagger in Development/Testing and `/health` in all environments.

## React Staff/Admin Console

1. Copy `frontend/react-app/.env.example` to `frontend/react-app/.env`.
2. Set `VITE_API_BASE_URL`, for example:

```env
VITE_API_BASE_URL=http://localhost:5000/api
```

3. Run:

```powershell
cd frontend\react-app
npm install
npm run dev
```

## Flutter Farmer App

Run:

```powershell
cd mobile\flutter_app
flutter pub get
flutter run --dart-define AGRIASSIST_API_BASE_URL=http://10.0.2.2:5000/api
```

Use `10.0.2.2` for Android emulator access to a backend running on the host machine. Use your LAN IP for a physical device.

## Tests

```powershell
dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj

cd frontend\react-app
npm test

cd ..\..\mobile\flutter_app
flutter analyze
flutter test
```

## Performance Baseline

With the backend running:

```powershell
k6 run -e API_BASE_URL=http://localhost:5000 performance\k6-basic.js
```

## Secrets

Local `.env` files are ignored. Production secrets must be configured in the hosting platform, not committed into source control.
'@

Write-NoBom (Join-Path $root "docs\database\er-diagram.md") @'
# ER Diagram

```mermaid
erDiagram
    AppUsers ||--o{ Farms : owns
    AppUsers ||--o{ CropPlanRequests : requests
    AppUsers ||--o{ FieldInspections : inspects
    AppUsers ||--o{ FarmTasks : assigned
    AppUsers ||--o{ ApprovalDecisions : decides
    AppUsers ||--o{ ResourceReservations : requests

    Farms ||--o{ Fields : contains
    Farms ||--o{ CropPlanRequests : has
    Farms ||--o{ FarmTasks : has
    Fields ||--o{ CropCycles : grows
    Fields ||--o{ CropPlanRequests : scopes
    Fields ||--o{ FieldInspections : inspected
    Fields ||--o{ IrrigationSchedules : schedules
    CropTypes ||--o{ CropCycles : used_by
    CropTypes ||--o{ CropPlanRequests : requested
    CropPlanRequests ||--o{ CropPlanRequestHistory : records

    FieldInspections ||--o{ InspectionObservations : records
    FieldInspections ||--o{ CropIssues : finds
    FieldInspections ||--o{ InspectionImages : uploads
    CropIssues ||--o{ FollowUpRecommendations : recommends

    ResourceCategories ||--o{ Resources : groups
    Suppliers ||--o{ Resources : supplies
    Resources ||--o{ InventoryStocks : stocked
    InventoryStocks ||--o{ StockTransactions : logs
    InventoryStocks ||--o{ ResourceReservations : reserves

    AgentWorkflows ||--o{ AgentSteps : contains
    AgentWorkflows ||--o{ AgentToolExecutions : executes
    AgentWorkflows ||--o{ AgentValidationResults : validates
    AgentWorkflows ||--o{ ApprovalDecisions : supports
```

The AgentWorkflow tables are present for later AI phases only. The current BASIC foundation does not execute AI workflows.
'@

Write-NoBom (Join-Path $root "docs\adr\0001-stack.md") @'
# ADR 0001: Required Stack

## Decision

Use ASP.NET Core 8 Web API, EF Core 8 with Npgsql/PostgreSQL, React + Vite for staff/admin web workflows, and Flutter + Provider for farmer/mobile workflows.

## Status

Accepted.

## Consequences

The backend owns secrets, persistence, auth, Cloudinary access, and business rules. React and Flutter remain API clients.
'@

Write-NoBom (Join-Path $root "docs\adr\0002-auth-rbac.md") @'
# ADR 0002: JWT Auth and RBAC

## Decision

Use local email/password login, BCrypt password hashes, JWT bearer authentication, and role-based authorization attributes.

## Status

Accepted.

## Consequences

The BASIC foundation can verify all role workflows without introducing external identity providers.
'@

Write-NoBom (Join-Path $root "docs\adr\0003-ai-disabled.md") @'
# ADR 0003: Disabled Agentic AI Client

## Decision

Create AgentWorkflow persistence and an `IAgenticAIClient` placeholder, but do not execute any LLM calls in this prompt.

## Status

Accepted.

## Consequences

Approval records can reference future workflows, while current behavior stays deterministic and non-AI.
'@

Write-NoBom (Join-Path $root "docs\adr\0004-cloudinary-boundary.md") @'
# ADR 0004: Cloudinary Boundary

## Decision

Upload inspection images through the ASP.NET Core API only. Client apps submit files to the backend; they do not hold Cloudinary secrets.

## Status

Accepted.

## Consequences

Cloudinary credentials remain server-side, and upload validation is centralized.
'@

Write-NoBom (Join-Path $root "docs\adr\0005-no-git-initialization.md") @'
# ADR 0005: No Local Git Initialization

## Decision

Do not initialize git in this workspace. Create source files and CI workflow YAML only.

## Status

Accepted for this build session.

## Consequences

Commit-based CI verification is intentionally skipped until the repository owner initializes git and pushes to a remote.
'@

Write-NoBom (Join-Path $root "docs\testing\verification-log.md") @'
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
'@

Write-NoBom (Join-Path $root "docs\testing\manual-test-checklist.md") @'
# Manual Test Checklist

- Start the API and confirm `/health` is healthy.
- Log into React with a seeded admin or staff account.
- Confirm dashboard metrics render.
- Create a farm, field, and preliminary crop plan request.
- Create an inspection, observation, and high severity crop issue.
- Confirm invalid image upload is rejected and real upload succeeds only when Cloudinary is configured.
- Create resource category/resource/stock and reserve stock.
- Create and approve a farm task and irrigation schedule.
- Run the Flutter app with `AGRIASSIST_API_BASE_URL` pointed at the API and confirm login, dashboard refresh, crop plan form, inspection camera/GPS actions, and reservation form.
'@

Write-NoBom (Join-Path $root "docs\ai-usage\basic-foundation-ai-boundary.md") @'
# AI Usage Boundary

This prompt builds the BASIC foundation only.

Implemented now:

- AgentWorkflow, AgentStep, AgentToolExecution, and AgentValidationResult persistence schema
- ApprovalDecision optional link to AgentWorkflow
- `IAgenticAIClient` placeholder service

Not implemented now:

- No prompt execution
- No LLM calls
- No autonomous tool execution
- No generated agronomy recommendations from AI

Future AI phases should treat these models as integration points and must keep backend-side validation and approval controls.
'@

Write-NoBom (Join-Path $root "docs\contributing.md") @'
# Contributing

## Rules

- Keep backend secrets in ignored `.env` files.
- Add backend business rules in services, not controllers.
- Keep React state in Context/hooks unless the spec changes.
- Keep Flutter state in Provider unless the spec changes.
- Do not enable AI execution in the BASIC foundation.
- Add or update tests for changed auth, authorization, validation, persistence, or workflow behavior.

## Verification Before Handoff

Run:

```powershell
dotnet build backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj
cd frontend\react-app
npm run build
npm test
cd ..\..\mobile\flutter_app
flutter analyze
flutter test
```
'@

Write-NoBom (Join-Path $root ".github\workflows\ci.yml") @'
name: AgriAssist CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore backend
        run: dotnet restore backend/AgriAssist.Api/AgriAssist.Api.csproj
      - name: Build backend
        run: dotnet build backend/AgriAssist.Api/AgriAssist.Api.csproj --configuration Release --no-restore
      - name: Test backend
        run: dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-build

  react:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: frontend/react-app
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: npm
          cache-dependency-path: frontend/react-app/package-lock.json
      - name: Install React dependencies
        run: npm ci
      - name: Build React app
        run: npm run build
      - name: Test React app
        run: npm test

  flutter:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: mobile/flutter_app
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
          cache: true
      - name: Install Flutter dependencies
        run: flutter pub get
      - name: Analyze Flutter app
        run: flutter analyze
      - name: Test Flutter app
        run: flutter test
'@

Write-NoBom (Join-Path $root "performance\k6-basic.js") @'
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 5,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
};

const apiBaseUrl = __ENV.API_BASE_URL || 'http://localhost:5000';

export default function () {
  const health = http.get(`${apiBaseUrl}/health`);
  check(health, {
    'health is 200': (response) => response.status === 200,
  });

  sleep(1);
}
'@
