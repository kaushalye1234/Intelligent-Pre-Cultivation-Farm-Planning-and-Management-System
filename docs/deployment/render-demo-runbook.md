# Group 04 Render demo runbook

This file describes a **demo** deployment using the existing Supabase PostgreSQL database, with the API, React app, and AI service hosted on Render.

## Before creating services

1. Merge the deployment files into `dev`, then select **New > Blueprint** in Render and connect this repository on `dev`.
2. Review `render.yaml` and the complete resource list before approving creation. The API connection is entered separately in Render and must point to the intended Supabase database. The old `agriassist-demo-db` resource remains declared to avoid deleting it during a Blueprint sync; the API no longer uses it. Render free web services sleep after 15 minutes without traffic. See [Render's Free plan limits](https://render.com/docs/free).
3. The AI service is a public web service on the free tier because free services cannot receive private-network requests. The AI workflow routes require generated bearer tokens; keep them in Render's environment settings and never put them in the React app or report. Render's private services are not free. See [Render private networking](https://render.com/docs/private-network) and [service plans](https://render.com/docs/compute-plans).
4. Enter the Supabase connection as `ConnectionStrings__DefaultConnection` in the API's Render environment. Use the SSL-enabled pooler connection verified for the intended database. Enter a valid `AI_MODEL` and `OPENAI_API_KEY` on the AI service. Configure `Weather__ApiKey` and the three `Cloudinary__...` settings on the API for live weather and image storage. `sync: false` keeps these values out of Git and does not overwrite existing values during later Blueprint updates; on existing services, enter missing settings in the dashboard or API rather than expecting a new prompt.
5. The example credentials previously found in tracked environment templates must be rotated before they are reused. Enter only newly rotated credentials in Render.

The blueprint generates JWT, API-to-AI, and AI-to-API secrets. It obtains service URLs from Render's `RENDER_EXTERNAL_URL`, and the React client appends `/api` to the API origin. The ASP.NET API converts Render's `postgresql://` database URL into Npgsql's keyword connection string and requires PostgreSQL outside Development/Testing.

## Deployment behavior and limitations

- `Database__ApplyMigrationsOnStart=false` preserves the existing Supabase schema during redeployment. Before deploying code that requires a schema change, review the migration, back up the intended database, and apply it through a separate authorized operation. Deploying application files does not copy local accounts or farm records into another database.
- Render terminates HTTPS before forwarding to the API. The API reads forwarded IP/scheme headers so HTTPS redirection observes Render's original request scheme. The blueprint trusts forwarded headers from all sources because Render proxy IPs vary; keep the API behind Render's proxy and do not copy this setting to an unproxied host.
- `ApiDocs__Enabled=true` makes `/swagger` public for evaluator access. Do not enable this on a production deployment without a security review.
- Free web services sleep while idle. Cold starts can make AI-backed requests exceed the API's current request timeouts. Verify the complete AI workflow after warm-up; upgrade the AI/API plans if the demo's required latency is not met.
- `/health` only reports process health; it does not prove database readiness or the approval workflow. Check migrations, authenticated API behavior, AI health, and one full officer approval flow separately.
- The AI service is bearer-token protected on its workflow routes but has a public host and health/docs surface. Do not expose any API tool token in browser code.

## Create the first staff login

Use the existing Supabase accounts when connecting this deployment. The Blueprint disables Admin bootstrap. The following procedure is only for a separately authorized empty database: the application does not seed an Admin account, and public farmer registration creates only the Farmer role. On a Render Free API service, use the guarded startup bootstrap instead of trying to open a Shell:

1. In the Render dashboard, open the `agriassist-api` service's **Environment** settings.
2. Temporarily add these variables. Use your group's chosen Admin name/email (the email must not already belong to another account) and a unique password of at least 12 characters, no more than 72 UTF-8 bytes, and different from the name/email. The normal password policy also rejects known compromised passwords.

   ```text
   AdminBootstrap__Enabled=true
   AdminBootstrap__FullName=<admin full name>
   AdminBootstrap__Email=<admin email>
   AdminBootstrap__Password=<new unique password>
   ```

3. Save the settings and wait for the API to redeploy. The API applies configured database migrations first, then creates an Admin only if no Admin account exists. Check the API logs for `Initial Admin account created from AdminBootstrap settings`.
4. Remove all four `AdminBootstrap__...` variables from the Render API service and save again. This triggers another deploy. Removing the variables does not remove the created Admin account.
5. Sign in at the React site using that Admin email/password. Create a separate Agricultural Officer account in the user-management screen for routine workflow testing.

If an Admin already exists, startup logs that bootstrap was skipped and does not change its password. Do not use this flow to reset an existing account. If enabled settings are incomplete or invalid, startup fails closed; correct them in Render and redeploy. Never put the password in Git, a screenshot, or chat. The Render service logs must not contain the password.

## After deployment

Build mobile deployments with `./scripts/build-render-mobile.ps1 -Target apk`
from the repository root. This sets the deployed HTTPS API URL explicitly;
the emulator URL in the source remains a local-development default. Rebuild
and reinstall older APKs to change their API URL.

Record the actual values in the report only after live verification:

- React URL: Render static-site `RENDER_EXTERNAL_URL`
- API health URL: API URL plus `/health`
- Swagger URL: API URL plus `/swagger`
- AI health URL: AI URL plus `/health` (health is not a credential check)
- Database migration outcome and deployed commit SHA
- One authenticated workflow run that progresses through proposal, officer approval, and farmer-visible final records

Keep evaluator credentials separate from the source repository. Do not claim the deployment is complete until the URLs respond and the end-to-end workflow is verified.
