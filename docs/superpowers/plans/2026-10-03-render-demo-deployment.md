# Render Demo Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Prepare a reviewable Render demo deployment for the React console, ASP.NET API, and FastAPI AI service, with the existing Supabase database and Cloudinary integration.

**Architecture:** Build the .NET 8 API and Python 3.12 AI service as separate Docker Web Services and host the Vite output as a Render Static Site. Connect the services through dashboard-configured HTTPS URLs and existing service-token checks; use the Supabase database already approved by the group. Keep cloud-resource creation and live migration behind a separate explicit operator approval.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core 8, PostgreSQL/Supabase, FastAPI, Python 3.12, React/Vite, Docker, Render Blueprints, PowerShell smoke checks, GitHub Actions.

**Spec:** docs/superpowers/specs/2026-10-02-render-demo-deployment-design.md

## Global Constraints

- Target a temporary demo on Render; use Singapore when the selected plan offers it.
- Use the existing Supabase PostgreSQL database and Cloudinary account; do not migrate data or create external resources in this implementation.
- A deployed API must require ConnectionStrings:DefaultConnection and use PostgreSQL; it must not silently fall back to EF InMemory.
- Apply EF migrations only as a deliberate operator action after verifying the database target.
- Keep service tokens, provider keys, database credentials, and JWT signing values in Render-managed secret configuration; never put them in source, Blueprint values, React, Flutter, or logs.
- The current AI settings support AI_PROVIDER=openai; use OPENAI_API_KEY and AI_MODEL. Do not configure Gemini without a separately reviewed feature change.
- API and AI containers must listen on Render's PORT; use 10000 as the local/default port when PORT is absent.
- Configure the API's App:ReactUrl to the exact deployed React origin and React's VITE_API_BASE_URL to the deployed API origin plus /api.
- Keep Swagger disabled in Production and preserve the explicit human-approval requirement for final tasks and irrigation schedules.
- Free-tier services may spin down; do not select a paid plan, paid add-on, or upgrade without explicit Group 04 approval.
- Do not commit .env files, credentials, build output, virtual environments, node_modules, __pycache__, or .pyc files.

## File Map

- Create backend/AgriAssist.Api/Dockerfile and backend/AgriAssist.Api/.dockerignore for a repeatable .NET 8 runtime image.
- Modify backend/AgriAssist.Api/Program.cs to make local .env loading optional/local-only and require PostgreSQL outside Development and Testing.
- Create backend/AgriAssist.Api.Tests/DeploymentConfigurationTests.cs for the deployed configuration boundary.
- Modify ai-service/Dockerfile to bind Uvicorn to PORT while retaining port 8001 as the local fallback.
- Create render.yaml for the two Docker Web Services and the React Static Site, with secrets left for dashboard configuration.
- Create docs/deployment/render-demo.md for dashboard configuration, explicit migration procedure, smoke checks, limits, and operator gates.
- Create scripts/deployment/verify-render-smoke.ps1 for public HTTPS, health, and API CORS checks.
- Modify STARTUP.md to link to the Render guide without changing local startup commands.
- Modify .github/workflows/ci.yml to build both Docker images on CI without publishing them or deploying services.

## Review Focus

1. Production with a missing database connection string must fail at startup rather than create ephemeral storage. Pin this in DeploymentConfigurationTests.
2. Render-injected settings must work when no .env file exists, and a local .env must not replace production values. Pin this in the API image smoke check with Production environment variables and no .env file.
3. Both containers must use a non-default PORT value when supplied. Pin this in container smoke checks by launching each image on an alternate port and requesting /health.
4. The deployed React origin must be the only configured non-local browser origin, and a preflight request must return that origin. Pin this in the PowerShell smoke check.
5. A first request after service spin-down may exceed the current AI/API timeout budget. Document the expected cold start and measure a real end-to-end request before claiming the demo is ready; never bypass workflow approval on timeout.
6. Production Swagger must remain unavailable. Pin this in DeploymentConfigurationTests.

## Task 1: Require durable storage in the deployed API and create its Docker image

**Files:**
- Modify: backend/AgriAssist.Api/Program.cs
- Create: backend/AgriAssist.Api.Tests/DeploymentConfigurationTests.cs
- Create: backend/AgriAssist.Api/Dockerfile
- Create: backend/AgriAssist.Api/.dockerignore

**Interfaces:**
- Consumes: ConnectionStrings:DefaultConnection, ASPNETCORE_ENVIRONMENT, ASPNETCORE_URLS, and Render's PORT environment variable.
- Produces: A Production API that fails fast without a PostgreSQL connection string and an image that listens on the supplied PORT (10000 when unset).

- [ ] **Step 1: Add the failing configuration tests**
  Add WebApplicationFactory tests named Production_startup_requires_postgres_connection_string, Production_uses_npgsql_when_connection_string_is_configured, and Production_does_not_map_swagger. Configure Production in each test and use a fake connection string for provider-selection assertions so no external database is contacted. The missing-connection test must assert that startup fails with a clear ConnectionStrings:DefaultConnection configuration error.

- [ ] **Step 2: Run the focused tests and verify the regression**
  Run: dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --filter FullyQualifiedName~DeploymentConfigurationTests
  Expected: Production_startup_requires_postgres_connection_string fails because Program.cs currently selects EF InMemory when the connection string is absent.

- [ ] **Step 3: Make environment loading safe and configure database providers**
  In Program.cs, load DotNetEnv only for a local Development process and only when a local .env file exists. Keep Testing on EF InMemory. Keep the existing local Development fallback if the connection string is intentionally omitted. In all other environments, throw a clear startup configuration exception when ConnectionStrings:DefaultConnection is blank; otherwise configure UseNpgsql. Do not change the existing Development/Testing Swagger policy or startup migration behavior.

- [ ] **Step 4: Run the focused tests and verify Production configuration**
  Run: dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --filter FullyQualifiedName~DeploymentConfigurationTests
  Expected: all three DeploymentConfigurationTests pass; the configured Production provider is Npgsql and the Production Swagger route returns 404.

- [ ] **Step 5: Add the API Docker image**
  Create a multi-stage .NET 8 Dockerfile using the API project as the build context. Publish AgriAssist.Api.dll into the ASP.NET 8 runtime image. Start Kestrel on http://0.0.0.0:${PORT:-10000}; add only required source files to .dockerignore.

- [ ] **Step 6: Build and smoke-test the API image**
  Run: docker build -f backend/AgriAssist.Api/Dockerfile -t agriassist-api:phase4 backend/AgriAssist.Api
  Run the image as Production with PORT=18080, a test-only 32+ character JWT signing value, a non-empty disposable PostgreSQL connection string, and no .env file. Request http://127.0.0.1:18080/health and expect HTTP 200. Run a second container without ConnectionStrings__DefaultConnection and expect it to exit with the configuration error. Do not use a real database credential in these local checks.

- [ ] **Step 7: Run backend tests and commit**
  Run: dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release
  Expected: exit code 0.
  Commit the API configuration, tests, Dockerfile, and .dockerignore as one focused change.

### Task 2: Make the AI container honor Render's port

**Files:**
- Modify: ai-service/Dockerfile

**Interfaces:**
- Consumes: Render PORT.
- Produces: Uvicorn on 0.0.0.0:PORT, falling back to port 8001 for existing local Docker instructions.

- [ ] **Step 1: Modify the Uvicorn startup command**
  Change the Docker CMD to use a shell-expanded PORT with a default of 8001 and exec Uvicorn as the container process. Keep the existing Python 3.12 base and dependency installation.

- [ ] **Step 2: Build and smoke-test the AI image**
  Run: docker build -t agriassist-ai:phase4 ai-service
  Run the image with PORT=18081 and map 18081:18081. Request http://127.0.0.1:18081/health and expect JSON status ok. Repeat without PORT using the documented local mapping 8001:8001 and expect the same response.

- [ ] **Step 3: Run AI service checks and commit**
  From ai-service, run: python -m compileall -q .; python -m pytest
  Expected: both commands exit 0. Commit the Dockerfile and any test adjustment required by the smoke-check result.

### Task 3: Define Render services without embedding credentials

**Files:**
- Create: render.yaml

**Interfaces:**
- Consumes: Dockerfiles from Tasks 1 and 2; frontend/react-app/package.json and package-lock.json.
- Produces: Render declarations for an API Docker Web Service, AI Docker Web Service, and React Static Site.

- [ ] **Step 1: Add the Render Blueprint**
  Declare two Docker Web Services and one Static Site. Set the API Production environment and /health check, the AI /health check, Singapore region when available, and the free demo plan. Use frontend/react-app as the React root, npm ci && npm run build as its build command, and dist as the publish directory.

- [ ] **Step 2: Declare only key names and non-secret defaults**
  List the API database, JWT, Cloudinary, optional weather, AI URL/token, tool token, and React-origin keys; list the AI provider/model/key, inbound token, backend URL, and tool token; list the React VITE_API_BASE_URL. Use Render dashboard prompts for secrets and assigned service URLs. Do not put actual values, real hostnames, or credentials in render.yaml.

- [ ] **Step 3: Check Blueprint structure and service alignment**
  Parse render.yaml with PyYAML and inspect each service's type, root, Dockerfile, context, health path, and environment key names against the sanitized .env.example files and current config.py. Expected: valid YAML, no embedded credential-looking values, and no unsupported Gemini setting.

- [ ] **Step 4: Commit the Blueprint**
  Commit render.yaml separately from the API and AI runtime changes.

### Task 4: Write the operator deployment guide and smoke script

**Files:**
- Create: docs/deployment/render-demo.md
- Create: scripts/deployment/verify-render-smoke.ps1
- Modify: STARTUP.md

**Interfaces:**
- Consumes: Render service names and configuration keys from Task 3.
- Produces: A reproducible manual deployment checklist and a read-only public smoke command.

- [ ] **Step 1: Document deployment prerequisites and dashboard configuration**
  Explain that the security-example cleanup PR must be merged and genuine values rotated before use; confirm the group owns or has permission to use the chosen Supabase project; set dashboard-managed secrets; set API-to-AI and AI-to-API URLs/tokens; set App__ReactUrl and VITE_API_BASE_URL only after Render assigns the public hostnames. Include the current OpenAI-only provider support and free-tier spin-down/cold-start expectations.

- [ ] **Step 2: Document migrations as an explicit operator action**
  Provide the Release dotnet ef database update command using the API project and environment-provided ConnectionStrings__DefaultConnection. Require the operator to verify the Supabase host/database identity and TLS before running it. State that this plan does not run the migration or place a connection string in the command or documentation.

- [ ] **Step 3: Implement the public smoke check**
  Add a PowerShell script requiring ReactUrl, ApiUrl, and AiUrl parameters. It must issue HTTPS GETs to the React root and both /health routes, then send an OPTIONS preflight to /api/auth/login with Origin=ReactUrl and Access-Control-Request-Method=POST. Fail nonzero if any status is not successful or Access-Control-Allow-Origin does not exactly match ReactUrl. Do not accept credentials or print secret values.

- [ ] **Step 4: Check script syntax and guide completeness**
  Run the PowerShell parser against scripts/deployment/verify-render-smoke.ps1 and inspect the guide for the correct environment-key names, explicit migration gate, no-secret rule, Swagger behavior, and free-tier limitations. Expected: no parser errors and all operator steps are reproducible without a secret literal.

- [ ] **Step 5: Link the guide from STARTUP.md and commit**
  Add one link under deployment or service operations; preserve all local startup commands. Commit the guide, smoke script, and link together.

### Task 5: Add image-build CI and run the complete project checks

**Files:**
- Modify: .github/workflows/ci.yml

**Interfaces:**
- Consumes: API and AI Dockerfiles from Tasks 1 and 2 and the existing backend, AI, React, and Flutter CI jobs.
- Produces: Pull-request CI that builds both images but never pushes or deploys them.

- [ ] **Step 1: Add a Docker image build job**
  Add an Ubuntu job that checks out the branch and runs docker build for the API and AI images using their checked-in Dockerfiles and contexts. Do not log in to a registry, push images, or use deployment credentials.

- [ ] **Step 2: Validate workflow syntax**
  Parse .github/workflows/ci.yml and render.yaml using the CI Python 3.12 environment with PyYAML installed. Expected: both parse without YAML errors.

- [ ] **Step 3: Run required checks**
  Run the repository checks required by AGENTS.md: backend restore/build/test, AI compileall/pytest, React npm ci/lint/build/test, and Flutter pub get/analyze/tests. Also build both Docker images. Expected: every applicable command exits 0; report any environment-blocked Flutter command separately and do not label it passing.

- [ ] **Step 4: Review changes and commit CI**
  Run git diff --check, scan tracked source for conflict markers, and confirm no .env, credential, image, or build output is staged. Commit the CI job as a separate focused commit. Push the feature branch and open a PR to dev only after all clean-checkout jobs pass.

## Deployment Gate (operator action, not part of code preparation)

- [ ] Confirm PR #64's sanitized environment examples are merged and any real exposed credentials are rotated by their owners.
- [ ] Obtain Group 04 approval for Render account use, the selected plan, and any possible charges before creating services. This plan assumes no paid tier.
- [ ] In Render, create/sync the Blueprint and enter secrets in the dashboard. Record generated public URLs without copying secrets into GitHub.
- [ ] Verify the Supabase target before applying the migration using the documented operator command.
- [ ] Run the HTTPS smoke script, then complete one real authenticated Member 4 workflow and record cold-start/request time. Verify no final task or irrigation schedule is created without officer approval.
- [ ] Record only verified API, React, AI, and Swagger/demo URLs in the group report. Do not invent deployment links, video, or other members' report sections.

## Final Review

After Tasks 1–5, compare every Delivery and Verification criterion in the spec with CI results and the deployment guide. The code-preparation PR can be merged only through normal review. Live provisioning, paid resource selection, database migration, and report/video publication remain separate operator actions and must not be inferred from code-preparation approval.
