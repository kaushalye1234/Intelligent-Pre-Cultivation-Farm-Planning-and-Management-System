# AgriAssist startup guide

Run each service in a separate PowerShell window from the repository root.

## 1. PostgreSQL (disposable local database)

Docker Desktop must be running. Start an isolated database:

```powershell
docker run -d --name agriassist-postgres-phase2 `
  -e POSTGRES_PASSWORD=CHANGE_THIS_LOCAL_PASSWORD `
  -e POSTGRES_DB=agriassist `
  -p 55432:5432 postgres:16
```

Apply migrations. Use `--connection` so the repository `.env` cannot redirect EF to a shared database:

```powershell
dotnet ef database update `
  --project .\backend\AgriAssist.Api `
  --startup-project .\backend\AgriAssist.Api `
  --connection "Host=127.0.0.1;Port=55432;Database=agriassist;Username=postgres;Password=CHANGE_THIS_LOCAL_PASSWORD;Ssl Mode=Disable"
```

## 2. AI service

Create `ai-service\.env` from `ai-service\.env.example`. Set the same random service token in `AI_SERVICE_TOKEN` and the backend tool token in `BACKEND_TOOL_TOKEN`. Set `BACKEND_TOOL_BASE_URL` to the API URL, normally `http://localhost:5087`.

Build and run the supported Python 3.12 container:

```powershell
cd ai-service
docker build -t agriassist-ai-local .
docker run --rm --name agriassist-ai-local --env-file .env -p 8001:8001 agriassist-ai-local
```

Verify it:

```powershell
Invoke-WebRequest http://127.0.0.1:8001/health -UseBasicParsing
```

## 3. Backend API

Create `backend\AgriAssist.Api\.env` from its example. Configure `AI__ServiceUrl=http://localhost:8001`, `AI__ServiceToken` to match `AI_SERVICE_TOKEN`, and `AI__ToolToken` to match `BACKEND_TOOL_TOKEN`. Use the local PostgreSQL connection string above.

```powershell
dotnet run --project .\backend\AgriAssist.Api --urls http://localhost:5087
```

Verify:

```powershell
Invoke-WebRequest http://127.0.0.1:5087/health -UseBasicParsing
```

Swagger is available at `http://localhost:5087/swagger` in Development.

## 4. React console

Set `VITE_API_BASE_URL=http://localhost:5087/api` in `frontend\react-app\.env`, then run:

```powershell
cd frontend\react-app
npm ci
npm run dev
```

Open the Vite URL shown in the terminal.

## 5. Flutter farmer app

With Flutter installed:

```powershell
cd mobile\flutter_app
flutter pub get
flutter run --dart-define AGRIASSIST_API_BASE_URL=http://10.0.2.2:5087/api
```

Use `http://localhost:5087/api` for Windows desktop, or replace `10.0.2.2` with the host LAN IP for a physical device.

## Stop local services

Press `Ctrl+C` in service terminals. Remove the disposable database when finished:

```powershell
docker rm -f agriassist-postgres-phase2
```

Never commit `.env` files or real credentials.
