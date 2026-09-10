$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$root = Join-Path $PSScriptRoot ".."

function Ensure-Dir($Path) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Write-NoBom($Path, $Content) {
    Ensure-Dir (Split-Path $Path -Parent)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

$ci = @'
name: Backend CI

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

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore backend
        run: dotnet restore backend/AgriAssist.Api/AgriAssist.Api.csproj

      - name: Build backend
        run: dotnet build backend/AgriAssist.Api/AgriAssist.Api.csproj --configuration Release --no-restore

      - name: Test backend
        run: dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-build
'@

Write-NoBom (Join-Path $root ".github\workflows\backend-ci.yml") $ci

Write-NoBom (Join-Path $root "performance\k6-basic.js") @'
import http from 'k6/http';
import { check, fail, sleep } from 'k6';

export const options = {
  vus: 5,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
};

const apiBaseUrl = __ENV.API_BASE_URL || 'http://localhost:5000';
const loginEmail = __ENV.LOGIN_EMAIL;
const loginPassword = __ENV.LOGIN_PASSWORD;

export function setup() {
  if (!loginEmail || !loginPassword) {
    fail('Set LOGIN_EMAIL and LOGIN_PASSWORD to run authenticated performance checks.');
  }

  const login = http.post(
    `${apiBaseUrl}/api/auth/login`,
    JSON.stringify({ email: loginEmail, password: loginPassword }),
    { headers: { 'Content-Type': 'application/json' } },
  );

  check(login, {
    'login is 200': (response) => response.status === 200,
    'login returns token': (response) => Boolean(response.json('accessToken')),
  });

  return { token: login.json('accessToken') };
}

export default function (data) {
  const health = http.get(`${apiBaseUrl}/health`);
  check(health, {
    'health is 200': (response) => response.status === 200,
  });

  const dashboard = http.get(`${apiBaseUrl}/api/dashboard/summary`, {
    headers: { Authorization: `Bearer ${data.token}` },
  });
  check(dashboard, {
    'dashboard is 200': (response) => response.status === 200,
    'dashboard has metrics': (response) => response.json('activeFarms') !== undefined,
  });

  sleep(1);
}
'@

Write-NoBom (Join-Path $root "docs\adr\0006-react-state-management.md") @'
# ADR 0006: React State Management

## Decision

Use React Context API and hooks for auth/session and local page state in the BASIC foundation.

## Status

Accepted.

## Consequences

The React app avoids Redux/Zustand until real complexity requires it. Server calls remain isolated behind the Axios API client.
'@

Write-NoBom (Join-Path $root "docs\adr\0007-flutter-provider-state.md") @'
# ADR 0007: Flutter Provider State

## Decision

Use Provider with `ChangeNotifier` for mobile session, API data, loading, and error state.

## Status

Accepted.

## Consequences

The Flutter app keeps state management simple and matches the BASIC foundation spec.
'@

Write-NoBom (Join-Path $root "docs\adr\0008-workflow-state-database-strategy.md") @'
# ADR 0008: Workflow-State Database Strategy

## Decision

Persist future AI workflow state in relational tables: AgentWorkflow, AgentStep, AgentToolExecution, and AgentValidationResult.

## Status

Accepted.

## Consequences

Future Agentic AI work can attach execution state to existing manual business workflows without changing the core operational tables.
'@

Write-NoBom (Join-Path $root "docs\adr\0009-deployment.md") @'
# ADR 0009: Deployment Boundary

## Decision

Deploy the ASP.NET Core API to a .NET-capable host, React as static Vite assets, PostgreSQL through Supabase, Cloudinary for image storage, and Flutter as platform builds.

## Status

Accepted.

## Consequences

Secrets remain server-side. React and Flutter need only public API base URLs.
'@

Write-NoBom (Join-Path $root "docs\adr\0010-ai-framework-template.md") @'
# ADR 0010: AI Framework Selection Template

## Status

Proposed for a later prompt.

## Context

Real Agentic AI is not implemented in the BASIC foundation.

## Decision Template

Future AI work must document:

- Selected orchestration framework
- Tool boundary and allow-list
- Prompt/version storage
- Human approval gates
- Evaluation strategy
- Failure and rollback behavior
- Security controls against unsafe tool execution
'@

Write-NoBom (Join-Path $root "docs\contributions\member-ai-readiness.md") @'
# Member AI Readiness

The BASIC foundation is intended to support four later AI member contributions. Real AI remains out of scope until those prompts.

## Member 1 - CropPlanning AI

Build on CropPlanRequest, CropPlanRequestHistory, CropType, Farm, Field, and AgentWorkflow.

## Member 2 - Inspection AI

Build on FieldInspection, CropIssue, FollowUpRecommendation, InspectionImage, and AgentWorkflow.

## Member 3 - Resource AI

Build on Resource, InventoryStock, StockTransaction, ResourceReservation, and AgentWorkflow.

## Member 4 - TaskApproval AI

Build on FarmTask, IrrigationSchedule, ApprovalDecision, and optional AgentWorkflow references.

## Guardrail

Do not bypass manual approval, RBAC, validation, or backend service rules when adding AI.
'@
