# AgriAssist Prompt 01 Design

## Context

The workspace is empty and is not currently a git repository. Prompt 01 requires a complete BASIC, non-AI foundation for AgriAssist AI using ASP.NET Core Web API, EF Core 8 with Npgsql, Supabase PostgreSQL, React + Vite, Flutter, JWT/RBAC, Cloudinary, documentation, tests, and a shared AI-ready workflow schema. Git initialization and commit evidence are intentionally skipped by user request.

## Selected Approach

Use a spec-complete monorepo foundation built phase by phase:

- `backend/AgriAssist.Api` targets .NET 8 and owns all database, auth, business workflow, Cloudinary, Swagger, health, logging, and AgenticAI placeholder behavior.
- `frontend/react-app` uses React + Vite with Context API and hooks for staff/admin workflows.
- `mobile/flutter_app` uses Flutter with Provider for farmer and field-officer mobile workflows.
- `docs`, `.github/workflows`, and performance scripts are created as repository-level supporting assets.

## Architecture

Clients call only the ASP.NET Core API. The API uses controllers, DTOs, validators, services, EF Core, and PostgreSQL. Future AI integration is represented only by database workflow state and a disabled `ExternalServices/AgenticAI` client placeholder; no LLM or agent execution is implemented in Prompt 01.

## Components

The backend is organized by business area:

- CropPlanning: farms, fields, crop types, crop cycles, and crop plan requests.
- Inspections: inspections, observations, issues, images, and follow-up recommendations.
- Resources: resources, categories, suppliers, stock, transactions, and reservations.
- TaskApproval: farm tasks, irrigation schedules, approval decisions, and status workflow.
- Shared: users, auth, pagination, dashboard summaries, common API responses, and audit fields.

## Data Flow

HTTP requests flow through controllers into DTOs, validators, services, business validation, EF Core, and Supabase PostgreSQL. Services enforce ownership, permissions, status transitions, duplicate prevention, and resource stock rules. Cloudinary uploads flow through ASP.NET Core only and store only image metadata in PostgreSQL.

## Error Handling

Centralized middleware returns safe structured errors for validation, authentication, authorization, not found, conflict, and unexpected server failures. Hidden chain-of-thought or internal AI reasoning is never stored. AI workflow errors store safe operational messages only.

## Testing

Verification is phase-based. Backend tests cover validators, services, ownership rules, auth, API integration paths, and resource stock behavior as far as feasible locally. React tests cover login, protected routes, forms, API error states, and list rendering. Flutter tests cover auth state, validation, navigation, and API state handling. External services such as Supabase and Cloudinary are documented and verified only when reachable with valid runtime configuration.

## Constraints

- Do not initialize git.
- Do not implement real AI calls.
- Do not expose secrets to React or Flutter.
- Keep `.env` ignored and `.env.example` committed-ready.
- Use .NET 8 target framework even though .NET 10 is also installed.
