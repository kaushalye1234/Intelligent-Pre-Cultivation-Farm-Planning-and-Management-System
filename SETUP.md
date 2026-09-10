# AgriAssist Setup

## Prerequisites

- .NET 8 SDK
- Node.js and npm
- Flutter SDK
- PostgreSQL database, Supabase recommended
- Cloudinary account for inspection image storage
- k6 only if you want to run the performance baseline

## Backend

1. Copy `backend/AgriAssist.Api/.env.example` to `backend/AgriAssist.Api/.env`.
2. Fill in database, JWT, Cloudinary, weather, and disabled AI placeholder settings.
3. From the repository root, run:

```powershell
dotnet restore backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet build backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet ef database update --project backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet run --project backend\AgriAssist.Api\AgriAssist.Api.csproj
```

The API exposes Swagger in Development/Testing and `/health` in all environments.

## React Staff/Admin Console

1. Copy `frontend/react-app/.env.example` to `frontend/react-app/.env`.
2. Set `VITE_API_BASE_URL`, for example:

```env
VITE_API_BASE_URL=http://localhost:5000/api
```

3. Run:

```powershell
cd frontend\react-app
npm install
npm run dev
```

## Flutter Farmer App

Run:

```powershell
cd mobile\flutter_app
flutter pub get
flutter run --dart-define AGRIASSIST_API_BASE_URL=http://10.0.2.2:5000/api
```

Use `10.0.2.2` for Android emulator access to a backend running on the host machine. Use your LAN IP for a physical device.

## Tests

```powershell
dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj

cd frontend\react-app
npm test

cd ..\..\mobile\flutter_app
flutter analyze
flutter test
```

## Performance Baseline

With the backend running:

```powershell
k6 run -e API_BASE_URL=http://localhost:5000 performance\k6-basic.js
```

## Secrets

Local `.env` files are ignored. Production secrets must be configured in the hosting platform, not committed into source control.