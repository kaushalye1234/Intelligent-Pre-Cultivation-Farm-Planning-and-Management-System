# CropFinding Timeout and Error Propagation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let synchronous CropFinding discovery run for approximately three minutes and return an exact, safe, stage-aware upstream failure when it cannot complete.

**Architecture:** FastAPI owns a 175-second operational budget and returns a structured error detail with the agent's active operation/stage and sanitized provider metadata. ASP.NET owns the 180-second outer deadline, deserializes the FastAPI detail into a typed exception, and maps timeout, provider, configuration, and transport failures to distinct API errors while React keeps its existing error and form-preservation behavior.

**Tech Stack:** Python 3.13, FastAPI, Pydantic, pytest, ASP.NET Core 8, `HttpClient`, `System.Text.Json`, xUnit, React/Vite.

---

### Task 1: Add stage-aware FastAPI error details

**Files:**
- Modify: `ai-service/schemas/crop_finding.py`
- Modify: `ai-service/agents/crop_finding_agent.py`
- Modify: `ai-service/main.py`
- Test: `ai-service/tests/test_crop_finding_api.py`
- Test: `ai-service/tests/test_crop_finding_agent.py`

**Step 1: Write failing API tests**

Add tests asserting provider, configuration, and overall-timeout responses use a detail object shaped like:

```python
{
    "code": "CROP_FINDING_PROVIDER_FAILURE",
    "message": "OpenAI web search exceeded the configured CropFinding timeout.",
    "requestId": "request-id",
    "operation": "web_search",
    "stage": 1,
    "attempt": 1,
    "category": "timeout",
    "upstreamStatus": 504,
    "providerErrorCode": "provider_timeout",
    "providerRequestId": "provider-request-id",
    "configuredTimeoutSeconds": 50,
    "effectiveTimeoutSeconds": 50,
}
```

Assert prompt/document text, service tokens, API keys, and raw provider exception messages are absent.

**Step 2: Run tests to verify failure**

Run: `python -m pytest tests/test_crop_finding_api.py -q`

Expected: FAIL because `detail` is currently a string and overall timeouts do not retain active stage information.

**Step 3: Add the error model and progress state**

Add a `CropFindingErrorDetail(CamelModel)` with nullable operation/stage/provider fields. Add `active_operation`, `active_stage`, and `active_attempt` read-only properties to `CropFindingAgent`, backed by a small `_set_progress(operation, stage, attempt=None)` helper. Set progress before web search, source retrieval, and each structured-analysis attempt.

Build safe details in `main._run_crop_finding`:

```python
detail = CropFindingErrorDetail(
    code="CROP_FINDING_TIMEOUT",
    message="CropFinding exceeded the 175-second operational deadline.",
    requestId=request_id,
    operation=agent.active_operation,
    stage=agent.active_stage,
    attempt=agent.active_attempt,
    category="timeout",
    configuredTimeoutSeconds=settings.crop_finding_overall_timeout_seconds,
    effectiveTimeoutSeconds=settings.crop_finding_overall_timeout_seconds,
)
raise HTTPException(status_code=504, detail=detail.model_dump(mode="json", by_alias=True, exclude_none=True))
```

For `LLMProviderError`, copy only its existing sanitized message and metadata. For `ProviderConfigurationError`, return a configuration-specific code without returning its raw message.

**Step 4: Run focused tests**

Run: `python -m pytest tests/test_crop_finding_api.py tests/test_crop_finding_agent.py -q`

Expected: PASS.

**Step 5: Commit**

```powershell
git add ai-service/schemas/crop_finding.py ai-service/agents/crop_finding_agent.py ai-service/main.py ai-service/tests/test_crop_finding_api.py ai-service/tests/test_crop_finding_agent.py
git commit -m "feat: return stage-aware CropFinding errors"
```

### Task 2: Align the 175/180-second budgets

**Files:**
- Modify: `ai-service/config.py`
- Modify: `ai-service/.env.example`
- Modify locally only: `ai-service/.env`
- Modify: `backend/AgriAssist.Api/appsettings.json`
- Modify: `backend/AgriAssist.Api/.env.example`
- Modify locally only: `backend/AgriAssist.Api/.env`
- Modify: `backend/AgriAssist.Api/ExternalServices/AgenticAI/AgenticAIClient.cs`
- Modify: `backend/AgriAssist.Api/Program.cs`
- Test: `ai-service/tests/test_crop_finding_api.py`
- Test: `backend/AgriAssist.Api.Tests/CropFindingAuthorizationIntegrationTests.cs`

**Step 1: Write failing default-budget tests**

Assert `Settings(_env_file=None).crop_finding_overall_timeout_seconds == 175`. Resolve the named `ICropFindingAIClient` from the ASP.NET test host and assert its global `HttpClient.Timeout` is infinite while the client configuration uses a 180-second linked cancellation token.

**Step 2: Run focused tests to verify failure**

Run the Python settings test and:

`dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~CropFinding_http_client --no-restore`

Expected: the old 105/110 defaults fail.

**Step 3: Update configuration**

Change FastAPI's default and example `CROP_FINDING_OVERALL_TIMEOUT_SECONDS` from `105` to `175`. Change ASP.NET's default, appsettings value, and example `AI__CropFindingTimeoutSeconds` from `110` to `180`; expand the clamp maximum enough to retain 180 exactly. Keep `HttpClient.Timeout = Timeout.InfiniteTimeSpan` for the typed CropFinding client so the linked 180-second token is authoritative. Apply the same values to ignored local `.env` files without exposing or staging them.

**Step 4: Run focused tests**

Expected: PASS with exact 175/180 values.

**Step 5: Commit**

```powershell
git add ai-service/config.py ai-service/.env.example backend/AgriAssist.Api/appsettings.json backend/AgriAssist.Api/.env.example backend/AgriAssist.Api/ExternalServices/AgenticAI/AgenticAIClient.cs backend/AgriAssist.Api/Program.cs backend/AgriAssist.Api.Tests/CropFindingAuthorizationIntegrationTests.cs ai-service/tests/test_crop_finding_api.py
git commit -m "fix: align CropFinding request budgets"
```

### Task 3: Preserve structured errors in the ASP.NET AI client

**Files:**
- Create: `backend/AgriAssist.Api/ExternalServices/AgenticAI/CropFindingAIException.cs`
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/CropFindingDtos.cs`
- Modify: `backend/AgriAssist.Api/ExternalServices/AgenticAI/AgenticAIClient.cs`
- Create: `backend/AgriAssist.Api.Tests/CropFindingAIClientTests.cs`

**Step 1: Write failing HTTP-client tests**

Use a recording/fake `HttpMessageHandler` to return structured `502`, `503`, and `504` JSON bodies. Assert `DiscoverReferencesAsync` throws `CropFindingAIException` retaining `StatusCode` and a typed `CropFindingErrorDetail`. Add malformed/empty-body coverage that retains the status with a safe fallback message.

**Step 2: Run tests to verify failure**

Run: `dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~CropFindingAIClientTests --no-restore`

Expected: FAIL because the client currently throws plain `HttpRequestException`.

**Step 3: Implement typed deserialization**

Add matching C# records for the FastAPI wrapper and detail. Add `CropFindingAIException` with `HttpStatusCode StatusCode` and `CropFindingErrorDetail? Detail`. On non-success responses, deserialize the body with the existing web JSON options and throw the typed exception; do not include raw response bodies in messages or logs.

**Step 4: Run focused tests**

Expected: PASS for all status and malformed-body cases.

**Step 5: Commit**

```powershell
git add backend/AgriAssist.Api/ExternalServices/AgenticAI/CropFindingAIException.cs backend/AgriAssist.Api/Dtos/CropPlanning/CropFindingDtos.cs backend/AgriAssist.Api/ExternalServices/AgenticAI/AgenticAIClient.cs backend/AgriAssist.Api.Tests/CropFindingAIClientTests.cs
git commit -m "feat: preserve CropFinding upstream errors"
```

### Task 4: Map exact failures at the ASP.NET service boundary

**Files:**
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropFindingService.cs`
- Modify: `backend/AgriAssist.Api.Tests/CropFindingServiceTests.cs`

**Step 1: Write failing mapping tests**

Extend the fake AI client to throw configured exceptions. Cover:

- upstream `504` or `category=timeout` -> `504 CROP_FINDING_TIMEOUT`;
- upstream `502` -> `502 CROP_FINDING_FAILED`;
- upstream `503` -> `503 CROP_FINDING_CONFIGURATION_UNAVAILABLE`;
- transport `HttpRequestException` without status -> `502 CROP_FINDING_UNAVAILABLE`;
- caller cancellation remains cancellation and is not remapped;
- local 180-second cancellation retains `CROP_FINDING_TIMEOUT`.

Assert messages name the safe operation/stage when present and retain the form-preservation sentence.

**Step 2: Run tests to verify failure**

Run: `dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~CropFindingServiceTests --no-restore`

Expected: FAIL because all upstream HTTP failures currently map to unavailable.

**Step 3: Implement deterministic mappings**

Catch `CropFindingAIException` before `HttpRequestException`. Construct a concise safe suffix such as `during web search (Sri Lankan evidence stage 1)` from the typed detail. Map status/category to the approved API codes, preserve the upstream request ID in structured logs, and never log raw payloads.

**Step 4: Run focused tests**

Expected: PASS for every mapping and cancellation case.

**Step 5: Commit**

```powershell
git add backend/AgriAssist.Api/Services/CropPlanning/CropFindingService.cs backend/AgriAssist.Api.Tests/CropFindingServiceTests.cs
git commit -m "fix: map CropFinding failures accurately"
```

### Task 5: Verify all affected systems and the real synchronous endpoint

**Files:**
- Test: `backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj`
- Test: `ai-service/tests`
- Test: `frontend/react-app`

**Step 1: Run backend verification**

```powershell
dotnet restore backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj
dotnet build backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-restore
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-build
```

Expected: all non-PostgreSQL tests pass; PostgreSQL-only tests may skip when their isolated connection is unavailable.

**Step 2: Run AI-service verification**

```powershell
cd ai-service
python -m compileall -q .
python -m pytest
```

Expected: all tests pass.

**Step 3: Run React verification**

```powershell
cd frontend/react-app
npm run lint
npm run build
npm test
```

Expected: commands succeed; existing non-blocking lint and chunk-size warnings may remain.

**Step 4: Restart local services and issue a real request**

Restart FastAPI and ASP.NET so they load the new code and local timeout values. Send an authenticated `DiscoverReferencesInput` directly to `http://127.0.0.1:8001/crop-finding/discover-references`, taking the bearer token from the ignored local environment without printing it.

Expected within 175 seconds: either HTTP 200 with source drafts, or a structured `502`/`503`/`504` detail containing request ID, exact operation/stage, category, safe message, and available provider metadata.

**Step 5: Run repository hygiene checks**

```powershell
git diff --check
rg -n --hidden --glob '!**/.git/**' --glob '!**/node_modules/**' --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/.venv/**' "^(<<<<<<<|=======|>>>>>>>)" .
git status --short --branch
```

Expected: no whitespace errors or conflict markers; unrelated `frontend/react-app/package-lock.json` and Vite changes remain preserved.
