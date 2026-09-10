# AgriAssist AI - Basic Foundation

AgriAssist is a BASIC, non-AI foundation for farm operations. This repository contains:

- ASP.NET Core 8 Web API backend with EF Core 8 and PostgreSQL/Supabase support
- React + Vite staff/admin console
- Flutter + Provider farmer mobile app
- Shared AgentWorkflow schema and disabled AgenticAI client placeholder
- Cloudinary integration path for inspection images

No LLM or agent execution is enabled in this prompt. The AI-facing schema and interfaces exist only so later prompts can build on them.

## Project Structure

```text
backend/AgriAssist.Api/          ASP.NET Core API
backend/AgriAssist.Api.Tests/    xUnit backend tests
frontend/react-app/              React + Vite web console
mobile/flutter_app/              Flutter farmer app
docs/                            ERD, ADRs, test notes, AI usage notes
performance/                     k6 baseline script
.github/workflows/               CI workflow YAML
```

## Local Commands

```powershell
dotnet build backend\AgriAssist.Api\AgriAssist.Api.csproj
dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj

cd frontend\react-app
npm install
npm run build
npm test

cd ..\..\mobile\flutter_app
flutter pub get
flutter analyze
flutter test
```

## Runtime Notes

- Backend secrets live in ignored `.env` files.
- React reads `VITE_API_BASE_URL`.
- Flutter defaults to `http://10.0.2.2:5000/api` for Android emulator use and can be overridden with `--dart-define AGRIASSIST_API_BASE_URL=...`.
- Cloudinary uploads require backend Cloudinary environment values.