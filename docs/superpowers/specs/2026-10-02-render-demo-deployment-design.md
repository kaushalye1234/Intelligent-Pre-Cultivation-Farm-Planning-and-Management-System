# Render Demo Deployment Design

**Date:** 2026-10-02  
**Status:** Proposed for user review  
**Scope:** Group 04 Phase 4 demo deployment architecture  
**Hosting target:** Render, with the API and AI service in Singapore where available

## Goal

Provide a repeatable, low-cost demo deployment for the existing AgriAssist application. Deploy the React console, ASP.NET API, and FastAPI AI service so the UI can call the API and the API can call the AI service. Continue using the project's existing Supabase PostgreSQL database and Cloudinary image storage.

This is a demo plan. It does not claim the application is production-ready, and it does not create Render resources or authorize charges.

## Current project facts

- The accepted deployment ADR selects a .NET-capable API host, static React assets, Supabase PostgreSQL, Cloudinary, and Flutter platform builds.
- The ASP.NET project targets .NET 8. It exposes a health endpoint at /health, configures the React CORS origin through App:ReactUrl, and only enables Swagger in Development or Testing.
- The AI service is FastAPI, has a /health endpoint, and already has a Dockerfile based on Python 3.12. That Dockerfile currently binds to port 8001 rather than Render's assigned port.
- The API reads its AI service URL and shared service/tool tokens from configuration. The AI service reads its incoming service token and backend tool URL/token from environment configuration. The current AI Settings type accepts AI_PROVIDER=openai; do not configure Gemini unless a separately reviewed code change adds support.
- The API invokes DotNetEnv.Env.Load() during startup. The deployment image must be tested without a .env file to confirm the process continues using Render-injected environment variables; if not, production startup behavior must be corrected before deployment.
- The project startup guide applies EF Core migrations explicitly. There is no automatic production migration step in API startup.
- Current Program.cs selects EF InMemory when the connection string is absent, regardless of environment, and ensures that in-memory database is created. The deployed profile must fail fast when its PostgreSQL connection string is missing so Render cannot appear healthy while using ephemeral storage.
- A separate open security PR replaces credential-looking values in tracked environment examples with placeholders. That change must be merged before those sanitized examples are used for deployment. Any values that were genuine must be rotated with their providers; replacing a tracked sample does not remove older Git history.

## Architecture

Officer browser → Render Static Site (React/Vite) → HTTPS API requests → Render Web Service (ASP.NET Core 8) → HTTPS with service token → Render Web Service (FastAPI/LangGraph). The AI service calls back to the API over HTTPS with the tool token. The API connects to Supabase PostgreSQL over TLS and Cloudinary over HTTPS. Flutter platform builds call the API over HTTPS.

The React console is public static content. The API and AI service are separate Docker Web Services. The API and AI service communicate over their public HTTPS URLs using the existing shared-token authentication. This keeps the design compatible with the demo/free plan, where private service networking is not available. Service tokens must never be placed in the React build or Flutter app.

## Render service layout

Create these Render services manually from the repository after implementation and review:

1. **AgriAssist API** — Docker Web Service from backend/AgriAssist.Api/Dockerfile, health check path /health, listening on Render's PORT (default 10000).
2. **AgriAssist AI** — Docker Web Service from ai-service/Dockerfile, health check path /health, listening on Render's PORT.
3. **AgriAssist React** — Static Site built from frontend/react-app; build command npm ci && npm run build; publish directory dist.

Use Singapore for the service region when the service/plan offers it. Keep the existing Supabase database in its configured region unless the group separately approves a database migration. The API connects to the existing database over TLS.

A repository Blueprint may describe the three services, but it must not contain credential values. Render does not interpolate a service's generated hostname into arbitrary environment-variable values; after service hostnames exist, set the cross-service URLs and CORS/API base URL in the Render dashboard. Do not assume that a checked-in Blueprint alone completes those links.

## Configuration and service links

Set secrets in the Render dashboard as secret environment variables, never in source control, Blueprint values, frontend variables, or build logs.

- API: database connection string, JWT signing secret and issuer/audience, Cloudinary credentials, weather API key if the feature is used, AI service URL and service token, AI tool token, and React origin.
- AI: AI_PROVIDER=openai and OPENAI_API_KEY for the current implementation, the same inbound service token as the API's AI token, public API base URL for backend tool calls, and the same tool token as the API's tool token.
- React: VITE_API_BASE_URL set to the API's public origin plus /api. This is public configuration and must contain no secret.
- Flutter builds: use the API's public HTTPS /api base URL through the existing build-time define. Do not embed AI or backend tool tokens.

The exact configuration key names must be taken from the checked-in examples and application settings. Set the API CORS origin to the final Render Static Site origin. Recheck both token pairs after all service URLs are assigned. Use unique random token values for the demo.

## Database setup and migrations

Use the group's existing Supabase database; deployment work must first confirm that the group has authority to use that database and that the connection string points to the intended project. Confirm TLS is enabled.

Do not apply migrations automatically during API startup. Before changing the database, review the pending EF migrations and verify the target host/database identity without printing credentials. Apply migrations once, as a deliberate operator action, and record the migration name and result. Render's free service does not provide the paid pre-deploy command needed for an automatic migration step, so the demo workflow uses a documented manual migration from a trusted local environment or one-off operator environment. Never run migrations against an unverified or shared database.

## Runtime behavior and failure handling

- All browser-facing traffic uses HTTPS. API and AI health checks use /health.
- Render services may sleep on free plans and cold-start on their next request. Demo users should expect a delay after inactivity; long AI requests must stay within the API and provider timeout budgets; verify that cold-start plus AI processing stays within the API's configured request timeout.
- Because free services cannot use private service networking, API-to-AI and AI-to-API calls use public HTTPS URLs and their existing service-token checks.
- A failing health check blocks or marks a deployment unhealthy; it must not bypass authentication or cause the API to seed or mutate production data.
- If Supabase, Cloudinary, the weather provider, or the AI provider is unavailable, the application should surface the existing safe failure behavior. Deployment must not fabricate analysis or bypass human approval.
- Keep Swagger disabled in Production as the current API behavior requires.

## Security and cost boundaries

- Do not commit real secrets or put them in frontend configuration. Use dashboard-managed secrets.
- Rotate any genuine credentials found in tracked examples. Sanitizing current files is not historical secret removal.
- Limit access to the Supabase and Render dashboards to the group members who need it. Do not share credentials in GitHub issues or reports.
- This design targets a temporary demonstration. Render free services can spin down and have resource/bandwidth limits; usage beyond included allowances or selecting paid resources may incur charges. Do not upgrade plans, add paid resources, or accept a paid add-on without explicit group approval.
- Do not store personal or sensitive farmer data in the demo beyond the minimum required for testing. Use approved test accounts and data.

## Delivery and verification criteria

Phase 4 implementation is ready for demo review when:

1. The reviewed security-example cleanup is merged, and any exposed real credentials have been rotated or confirmed as non-credentials by their owners.
2. A deployed API without ConnectionStrings:DefaultConnection fails startup with a clear configuration error; it never falls back to EF InMemory outside Testing or an explicitly supported local Development profile.
3. API and AI Docker images build from a clean checkout and both processes bind to Render's assigned port. The API starts with Render-injected environment values when no .env file is present.
4. The React static build succeeds and receives only the public API URL.
5. Render dashboard configuration links the React origin, API URL, AI URL, and matching service-token pairs without exposing secrets.
6. The operator has reviewed the exact Supabase target and applied pending migrations explicitly.
7. Public HTTPS /health checks pass for API and AI; the React site loads; browser API calls pass CORS; an authenticated end-to-end workflow reaches the AI service and returns the existing safe response.
8. Swagger remains unavailable in Production, and no service can create final tasks or irrigation schedules without explicit human approval.
9. The group records actual deployed URLs, the deployment date, known cold-start limitations, and demo evidence only after a real deployment succeeds.

## Out of scope

- Provisioning Render, Supabase, or Cloudinary resources.
- Applying production migrations or migrating the database.
- Production SLA, scaling, private networking, backups/restore exercises, monitoring subscriptions, or paid plans.
- Publishing a demo video or filling in other members' report sections.
- Changing application product behavior unrelated to deployment readiness.
- Claiming Flutter is deployed as a hosted web app; Flutter remains a platform build that points at the public API.


## References

- [Render Blueprint specification](https://render.com/docs/blueprint-spec) — service definitions, environment variables, regions, and hostname interpolation limits.
- [Render web services](https://render.com/docs/web-services) and [Docker](https://render.com/docs/docker) — container port binding and Docker runtime.
- [Render health checks](https://render.com/docs/health-checks) — health-check path behavior.
- [Render environment variables](https://render.com/docs/environment-variables) — service configuration and secrets.
- [Render free instance limits](https://render.com/docs/free) and [pricing](https://render.com/pricing) — spin-down and possible usage charges.
- [Render deploys](https://render.com/docs/deploys) — deploy lifecycle and pre-deploy command availability.
- Project deployment decision: docs/adr/0009-deployment.md.
