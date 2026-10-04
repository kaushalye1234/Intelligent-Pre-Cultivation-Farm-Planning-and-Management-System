# CropFinding Timeout and Error Propagation Design

## Scope

Keep CropFinding reference discovery synchronous while allowing the operation to run for approximately three minutes. Preserve the existing Admin-only authorization, source-review workflow, form values, completed review decisions, and rule filtering. Do not persist AI drafts or create verified references automatically.

## Root cause

The CropFinding FastAPI workflow currently has a 105-second overall timeout while its web search, source retrieval, structured analysis, fallback stage, and controlled retry can legitimately require longer. The ASP.NET client previously also competed with that budget through `HttpClient.Timeout`.

After a non-success FastAPI response, `AgenticAIClient` throws a generic `HttpRequestException`. `CropFindingService` maps every such exception to `CROP_FINDING_UNAVAILABLE`, so FastAPI responses for provider failures, configuration failures, and operation timeouts all become the misleading message that the service is unavailable. The FastAPI service itself is healthy and reachable.

## Design

Set the FastAPI operational deadline to 175 seconds and the ASP.NET CropFinding hard deadline to 180 seconds. The five-second separation allows FastAPI to serialize a structured timeout response before ASP.NET cancels the synchronous request. The React API client remains without its own shorter timeout.

Track the active CropFinding operation and evidence stage on the agent. The operation values are `web_search`, `source_retrieval`, and `structured_analysis`; the stage value distinguishes Sri Lankan evidence stage 1 from international fallback stage 2. Overall deadline errors use the last active operation and stage instead of reporting only `overall_request`.

FastAPI will return a structured, safe error detail containing:

- a stable error code and readable message;
- the CropFinding request ID;
- operation, stage, attempt, and category when known;
- upstream HTTP status, provider error code, and provider request ID when supplied by the provider;
- configured and effective timeout values when relevant.

Raw prompts, retrieved document contents, credentials, and unfiltered provider exception messages must never cross the service boundary.

The ASP.NET client will deserialize this envelope for non-success CropFinding responses and throw a typed exception that retains the upstream HTTP status and safe detail. `CropFindingService` will map errors as follows:

- FastAPI `504` or timeout category: `504 CROP_FINDING_TIMEOUT`;
- FastAPI `502` or provider/analysis failure: `502 CROP_FINDING_FAILED`;
- FastAPI `503` or configuration failure: `503 CROP_FINDING_CONFIGURATION_UNAVAILABLE`;
- transport or connection failure without an HTTP response: `502 CROP_FINDING_UNAVAILABLE`.

The returned ASP.NET message will name the failed operation and stage when available and append the existing guarantee that form values were not changed. React will continue using its existing API error rendering and preservation of form values and completed review decisions.

## Configuration

Update committed defaults and examples to the 175-second FastAPI operational budget and the 180-second ASP.NET hard timeout. Update the ignored local environment values used for verification without exposing or committing secrets. Keep individual operation timeouts bounded by the overall deadline and retain the current controlled retry policy.

## Verification

Add Python regression tests for the 175-second default, stage-aware overall timeout payloads, structured provider failures, and configuration failures. Add .NET regression tests for the 180-second hard timeout, structured error deserialization, every HTTP/error mapping, and genuine connection failures.

Run the focused tests, full backend and AI-service suites, React tests/build because it consumes the error contract, `git diff --check`, and a conflict-marker scan. Finally, issue a real authenticated local request directly to the FastAPI CropFinding endpoint and verify that it either completes or returns the exact structured safe upstream failure within the configured deadline.
