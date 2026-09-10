# Contributing

## Rules

- Keep backend secrets in ignored `.env` files.
- Add backend business rules in services, not controllers.
- Keep React state in Context/hooks unless the spec changes.
- Keep Flutter state in Provider unless the spec changes.
- Do not enable AI execution in the BASIC foundation.
- Add or update tests for changed auth, authorization, validation, persistence, or workflow behavior.

## Verification Before Handoff

Run:

```powershell
dotnet build backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj
cd frontend\react-app
npm run build
npm test
cd ..\..\mobile\flutter_app
flutter analyze
flutter test
```