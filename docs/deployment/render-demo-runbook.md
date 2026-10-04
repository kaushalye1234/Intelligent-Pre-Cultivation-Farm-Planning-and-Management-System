# Group 04 Render demo runbook

This file describes a reproducible **demo** deployment for the repository's current API, React app, AI service, and PostgreSQL schema. It does not mean a Render account or service has been created.

## Before creating services

1. Merge the deployment files into `dev`, then select **New > Blueprint** in Render and connect this repository on `dev`.
2. Review `render.yaml` and the complete resource list before approving creation. It selects free web services and a free PostgreSQL database. Render says free web services sleep after 15 minutes without traffic and free databases expire after 30 days; after expiry the database is inaccessible and can be deleted if not upgraded during its grace period. Use demo data only and export anything the group needs before expiry. See [Render's Free plan limits](https://render.com/docs/free).
3. The AI service is a public web service on the free tier because free services cannot receive private-network requests. The AI workflow routes require generated bearer tokens; keep them in Render's environment settings and never put them in the React app or report. Render's private services are not free. See [Render private networking](https://render.com/docs/private-network) and [service plans](https://render.com/docs/compute-plans).
4. Enter a valid OpenAI model name and the group's OpenAI API key when prompted. Cloudinary and weather are optional; configure them later in the API's environment if the group wants image uploads or live weather.
5. The example credentials previously found in tracked environment templates must be rotated before they are reused. Enter only newly rotated credentials in Render.

The blueprint generates JWT, API-to-AI, and AI-to-API secrets. It obtains service URLs from Render's `RENDER_EXTERNAL_URL`, and the React client appends `/api` to the API origin. The ASP.NET API converts Render's `postgresql://` database URL into Npgsql's keyword connection string and requires PostgreSQL outside Development/Testing.

## Deployment behavior and limitations

- `Database__ApplyMigrationsOnStart=true` runs EF migrations before the API begins listening. This is selected because Render pre-deploy commands require a paid plan. Keep the API at one instance for this demo; migrations are not coordinated across multiple replicas.
- Render terminates HTTPS before forwarding to the API. The API reads forwarded IP/scheme headers so HTTPS redirection observes Render's original request scheme. The blueprint trusts forwarded headers from all sources because Render proxy IPs vary; keep the API behind Render's proxy and do not copy this setting to an unproxied host.
- `ApiDocs__Enabled=true` makes `/swagger` public for evaluator access. Do not enable this on a production deployment without a security review.
- Free web services sleep while idle. Cold starts can make AI-backed requests exceed the API's current request timeouts. Verify the complete AI workflow after warm-up; upgrade the AI/API plans if the demo's required latency is not met.
- `/health` only reports process health; it does not prove database readiness or the approval workflow. Check migrations, authenticated API behavior, AI health, and one full officer approval flow separately.
- The AI service is bearer-token protected on its workflow routes but has a public host and health/docs surface. Do not expose any API tool token in browser code.

## After deployment

Record the actual values in the report only after live verification:

- React URL: Render static-site `RENDER_EXTERNAL_URL`
- API health URL: API URL plus `/health`
- Swagger URL: API URL plus `/swagger`
- AI health URL: AI URL plus `/health` (health is not a credential check)
- Database migration outcome and deployed commit SHA
- One authenticated workflow run that progresses through proposal, officer approval, and farmer-visible final records

Keep evaluator credentials separate from the source repository. Do not claim the deployment is complete until the URLs respond and the end-to-end workflow is verified.
