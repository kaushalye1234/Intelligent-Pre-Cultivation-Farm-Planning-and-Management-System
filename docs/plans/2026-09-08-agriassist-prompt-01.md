# AgriAssist Prompt 01 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the complete BASIC non-AI AgriAssist foundation across backend, React, Flutter, docs, tests, CI YAML, and performance baseline.

**Architecture:** The ASP.NET Core API is the only integration point for React and Flutter. It owns authentication, RBAC, business services, EF Core persistence, Cloudinary access, and AI-ready workflow state. React and Flutter remain normal clients and never receive backend secrets.

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core 8, Npgsql, PostgreSQL/Supabase, JWT, DotNetEnv, Swagger/OpenAPI, React + Vite, Context API, Flutter, Provider, Cloudinary, xUnit, Vitest, Flutter tests, k6.

---

### Phase 1: Repository Foundation and Backend Skeleton

**Files:**
- Create: `backend/AgriAssist.Api/AgriAssist.Api.csproj`
- Create: `backend/AgriAssist.Api/Program.cs`
- Create: `backend/AgriAssist.Api/Middleware/ExceptionHandlingMiddleware.cs`
- Create: `backend/AgriAssist.Api/.env.example`
- Create: `.env.example`
- Create: `.gitignore`
- Create: `SETUP.md`

**Steps:**
1. Scaffold the .NET 8 Web API project.
2. Add required packages: EF Core 8, Npgsql, DotNetEnv, JWT auth, Swagger.
3. Configure environment loading, CORS, Swagger, health, controllers, logging, and centralized exception middleware.
4. Run `dotnet restore`.
5. Run `dotnet build`.

### Phase 2: Shared Domain, Auth, and Database Foundation

**Files:**
- Create/modify backend `Models/Shared`, `Dtos/Shared`, `Validators/Shared`, `Services/Shared`, `Data/AppDbContext.cs`, `Data/SeedData.cs`

**Steps:**
1. Add users, roles, audit fields, auth DTOs, JWT options, password hashing, and auth/user services.
2. Add register, login, current profile, admin user list/detail, activate/deactivate, and role management endpoints.
3. Add EF configurations, indexes, and seed data.
4. Add migrations.
5. Run backend build and tests.

### Phase 3: CropPlanning Backend

**Files:**
- Create/modify backend `Models/CropPlanning`, `Dtos/CropPlanning`, `Validators/CropPlanning`, `Services/CropPlanning`, `Controllers/CropPlanning`

**Steps:**
1. Add Farm, Field, CropType, CropCycle, CropPlanRequest models.
2. Add DTOs, validators, service rules, CRUD, query support, history/status fields, and preliminary crop plan request operation.
3. Enforce farmer ownership, valid dates, positive area/budget, valid FKs, and duplicate active request prevention.
4. Run backend build and targeted tests.

### Phase 4: Inspections Backend and Cloudinary

**Files:**
- Create/modify backend `Models/Inspections`, `Dtos/Inspections`, `Validators/Inspections`, `Services/Inspections`, `Controllers/Inspections`, `ExternalServices/Cloudinary`

**Steps:**
1. Add inspection models, DTOs, validators, services, CRUD, query support, status/history, and escalation operation.
2. Add Cloudinary options/service/upload result and ASP.NET-only image upload.
3. Validate MIME type, extension, max size, non-empty files, and authorization.
4. Run backend build and targeted tests.

### Phase 5: Resources Backend

**Files:**
- Create/modify backend `Models/Resources`, `Dtos/Resources`, `Validators/Resources`, `Services/Resources`, `Controllers/Resources`

**Steps:**
1. Add resources, categories, suppliers, stock, transactions, and reservations.
2. Add CRUD, query support, low-stock view, inventory history, Reserve and Release operations.
3. Enforce no negative stock, reservation availability, transactions, and concurrency token handling.
4. Run backend build and targeted tests.

### Phase 6: TaskApproval and AgentWorkflow Backend

**Files:**
- Create/modify backend `Models/TaskApproval`, `Models/Shared/AgentWorkflow*`, `Dtos/TaskApproval`, `Services/TaskApproval`, `ExternalServices/AgenticAI`

**Steps:**
1. Add farm tasks, irrigation schedules, approval decisions, and nullable `AgentWorkflowId`.
2. Add AgentWorkflow, AgentStep, AgentToolExecution, and ValidationResult models.
3. Add approve, reject, and request-revision operations.
4. Add disabled AgenticAI client placeholder.
5. Run backend build and targeted tests.

### Phase 7: Backend Dashboard, Search, Tests, and Migration Verification

**Files:**
- Create/modify dashboard controllers/services and backend test project.

**Steps:**
1. Add dashboard summary endpoints.
2. Confirm query support for required entities.
3. Add focused xUnit tests for validators, services, auth, ownership, authorization, and stock behavior.
4. Run `dotnet test`.

### Phase 8: React + Vite Staff App

**Files:**
- Create: `frontend/react-app`

**Steps:**
1. Scaffold Vite React app.
2. Add Axios client, auth context, protected routes, role navigation, dashboard, and all four feature page groups.
3. Add loading, error, empty states, and form validation.
4. Add Vitest tests for login, protected route, form validation, API errors, and basic list pages.
5. Run npm build and tests.

### Phase 9: Flutter Mobile App

**Files:**
- Create: `mobile/flutter_app`

**Steps:**
1. Scaffold Flutter app.
2. Add Provider state, auth API client, secure token storage, profile, farmer dashboard, farms/fields, crop requests, history/status, inspection summaries, resource status, task/schedule/approval status, and field-officer inspection preparation.
3. Add form validation, navigation, and API state tests.
4. Run Flutter tests and build where feasible.

### Phase 10: Documentation, CI YAML, Performance Baseline, and Final Verification

**Files:**
- Create: `docs/database/er-diagram.md`
- Create: `docs/adr/0001-react-state-management.md`
- Create: `docs/adr/0002-flutter-state-management.md`
- Create: `docs/adr/0003-database-schema-strategy-for-agent-workflow-state.md`
- Create: `docs/adr/0004-deployment-platform.md`
- Create: `docs/adr/0005-agentic-ai-framework-and-orchestration.md`
- Create: `docs/testing/*`
- Create: `docs/ai-usage/*`
- Create: `docs/contributions/*`
- Create: `.github/workflows/backend-ci.yml`
- Create: `performance/k6-basic.js`
- Modify: `README.md`

**Steps:**
1. Write ER diagram covering all entities and AI-ready workflow relationships.
2. Write ADRs.
3. Add testing, AI usage, contribution, setup, and deployment documentation.
4. Add backend CI workflow YAML without secrets.
5. Add k6 baseline script.
6. Run final build/test verification for backend, React, and Flutter.
7. Report exact verification results and known limitations.
