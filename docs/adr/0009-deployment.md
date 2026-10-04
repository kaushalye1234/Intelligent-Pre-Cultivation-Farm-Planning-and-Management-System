# ADR 0009: Deployment Boundary

## Decision

Use Render for the Group 04 demonstration: ASP.NET Core API, React static Vite site, FastAPI AI service, and PostgreSQL. Keep Cloudinary and weather credentials optional. Flutter remains a separately built mobile client using the deployed API URL.

## Status

Accepted.

## Consequences

Secrets remain server-side. React and Flutter need only the public API URL. `render.yaml` is the reproducible demo blueprint and `docs/deployment/render-demo-runbook.md` records its free-tier limits and operational steps. The Render blueprint is a demo configuration, not a production availability design: the AI web service is public but token-protected, free services sleep, and the free database expires after 30 days. See the [Render demo runbook](../deployment/render-demo-runbook.md).
