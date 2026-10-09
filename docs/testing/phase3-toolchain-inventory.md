# Phase 3 toolchain inventory

Recorded from the Phase 3 project files and available local verification evidence on 2026-10-01. Values marked “not captured” are deliberately left unspecified rather than inferred.

| Area | Version used or configured | Evidence and precision |
|---|---|---|
| ASP.NET API / EF Core | Target framework `net8.0`; EF Core and Npgsql EF provider `8.0.11`; ASP.NET JWT bearer `8.0.20` | Project file. Backend CI installs .NET SDK `8.0.x`; its exact patch is not pinned. The local shell reported SDK `10.0.302` during troubleshooting, which is not the CI toolchain. |
| AI service runtime | Python `3.12`; Docker base `python:3.12-slim` | CI selects Python `3.12`; Dockerfile uses the floating `3.12-slim` tag, so the patch and image digest are not fixed. |
| AI direct dependencies | FastAPI `0.115.6`; Uvicorn `0.34.0`; Pydantic `2.10.4`; pydantic-settings `2.7.1`; LangGraph `0.2.60`; httpx `0.28.1`; google-generativeai `0.8.3`; OpenAI `1.58.1`; pytest `8.3.4`; pytest-asyncio `0.25.0` | Exact direct pins in `ai-service/requirements.txt`. Transitive dependencies are not captured in a lock file, so their resolved versions are not claimed. |
| React toolchain | Node.js `22` in CI; npm lockfile version `3` | CI selects Node `22`; exact Node/npm patch versions are not pinned. |
| React direct packages | React and React DOM `19.2.8`; Vite `8.2.2`; Vitest `5.0.0`; TypeScript `6.0.3`; Axios `1.20.0`; React Router `7.18.3`; lucide-react `1.43.0`; oxlint `1.82.0` | Resolved versions in `frontend/react-app/package-lock.json`. |
| Flutter | Flutter `3.47.4` | Local verification log records this SDK version. CI follows the Flutter stable channel and does not pin an exact SDK patch. |
| PostgreSQL | Major version `16` | CI service and startup guide use `postgres:16`. The patch version and image digest are not pinned. |
| Docker | Exact Docker Engine/Desktop version not captured | The verification records that services ran in Docker but does not include `docker version` output. |
| Android/JDK | Android SDK/JDK details are recorded in the release evidence; exact runtime patch versions were not consolidated here | No new exact value is asserted without command output from the build environment. |

## Reproducibility limits

The repository pins .NET and Python major/minor toolchain lines in CI and pins direct AI dependencies, while Docker image tags and CI SDK selectors can resolve to newer patch releases. Capture `dotnet --info`, `python --version`, `node --version`, `npm --version`, `flutter --version`, `docker version`, and the resolved PostgreSQL image digest during the final clean-checkout run if the submission requires exact machine-level versions.