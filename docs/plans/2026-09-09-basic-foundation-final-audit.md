# AgriAssist BASIC Foundation Final Audit Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Audit, test, and repair the current BASIC non-AI AgriAssist foundation without implementing real Agentic AI.

**Architecture:** Keep the existing feature-separated ASP.NET Core API, React/Vite console, and Flutter/Provider app. Make only targeted fixes for verified gaps in authorization, workflow state, docs, CI naming, and performance baseline coverage.

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core 8, Npgsql/PostgreSQL/Supabase, React + Vite + Context API, Flutter + Provider, Cloudinary via backend.

---

### Task 1: Complete Repository Audit

**Files:**
- Read: repository root, backend, frontend, mobile, docs, CI, performance, environment templates

**Steps:**
1. List root folders and important files.
2. Inspect `Program.cs`, `AppDbContext`, controllers, services, validators, React source, Flutter source, docs, CI, and performance scripts.
3. Search for TODO/mock/placeholder markers and hard-coded secrets.
4. Record missing or incomplete audit requirements.

### Task 2: Fix Confirmed BASIC Foundation Gaps

**Files:**
- Modify: `backend/AgriAssist.Api/Services/TaskApproval/TaskApprovalService.cs`
- Modify: `backend/AgriAssist.Api/Services/TaskApproval/ITaskApprovalService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/TaskApproval/TaskApprovalController.cs`
- Modify: `backend/AgriAssist.Api/Services/Resources/IResourceService.cs`
- Modify: `backend/AgriAssist.Api/Services/Resources/ResourceService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/Resources/ResourcesController.cs`
- Create: focused backend tests for repeated approvals and reservation cancellation

**Steps:**
1. Block repeated task/schedule decisions unless the entity is pending/revision state.
2. Add manual irrigation schedule reject/revision endpoints to match task approval operations.
3. Add resource reservation cancellation for active reservations.
4. Add tests for double release/cancel and repeated approval.
5. Run backend build/tests.

### Task 3: Fix Audit Artifacts

**Files:**
- Create: `.github/workflows/backend-ci.yml`
- Modify: `performance/k6-basic.js`
- Create: missing ADRs and `docs/contributions/`

**Steps:**
1. Add the requested CI filename while keeping no embedded secrets.
2. Expand k6 to include `/health`, authenticated login, and authenticated dashboard read.
3. Add ADRs for React state, Flutter state, workflow-state DB strategy, deployment, and future AI framework selection.
4. Add member contribution readiness docs.

### Task 4: Run Final Verification

**Commands:**
- `dotnet restore`
- `dotnet build`
- `dotnet test`
- backend HTTP smoke scripts for auth/RBAC/business rules
- `npm install`
- `npm run build`
- `npm test`
- `flutter pub get`
- `flutter analyze`
- `flutter test`
- `flutter build apk --debug` if the environment completes it
- `k6 run` only if k6 is installed

**Steps:**
1. Run each command sequentially to avoid Windows build file locks.
2. Start the API only for HTTP smoke checks, then stop it.
3. Mark anything unavailable or blocked as `NOT VERIFIED`.
