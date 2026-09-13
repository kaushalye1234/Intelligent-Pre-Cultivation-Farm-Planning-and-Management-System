# Member 2 Inspections AI Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the Member 2 inspections and field-analysis workflow across AI service, ASP.NET, React, Flutter, and tests using evidence-linked stored data only.

**Architecture:** ASP.NET remains the public API and workflow orchestrator. The Python AI service adds a `CropFieldAnalysisAgent` endpoint that calls allow-listed ASP.NET internal tools, receives only stored text/context and image metadata, and returns a validated envelope persisted in the existing `AgentStep.OutputJson`. React and Flutter call ASP.NET only.

**Tech Stack:** ASP.NET Core 8, EF Core, xUnit, FastAPI, LangGraph, Pydantic, pytest, React 19/Vite/Vitest, Flutter Provider/http/image_picker/geolocator.

---

### Task 1: AI Service Field Analysis Schema and Agent

**Files:**
- Create: `ai-service/schemas/field_analysis.py`
- Create: `ai-service/tools/inspection_tools.py`
- Create: `ai-service/agents/crop_field_analysis_agent.py`
- Modify: `ai-service/graph/workflow_graph.py`
- Modify: `ai-service/main.py`
- Test: `ai-service/tests/test_crop_field_analysis_agent.py`

**Step 1: Write failing pytest cases**

Cover golden output, missing inspection data, evidence IDs preserved, no invented issue IDs, malformed LLM output, prompt injection in officer notes, provider timeout, tool failure, and metadata-only image handling.

Run: `cd ai-service; pytest tests/test_crop_field_analysis_agent.py -q`
Expected: FAIL because files do not exist.

**Step 2: Implement schema and tools**

Add `FieldAnalysisInput`, `FieldCondition`, `OpenIssueSummary`, and `CropFieldAnalysisOutput`. Add `InspectionTools` methods for `GetFieldDetails`, `GetCropCycleDetails`, `GetRecentInspections`, `GetOpenCropIssues`, `GetInspectionImageMetadata`, and `GetCropReferenceProfile`.

**Step 3: Implement agent**

Fetch tools in the allow-listed order. If no inspections exist, return `Analyzed` with `requiresHumanReview = true`, missing-evidence warning, and no fabricated facts. If provider is configured, ask for JSON only and validate conclusions against fetched inspection IDs and issue IDs. If provider is absent, return deterministic evidence summary.

**Step 4: Wire route and graph**

Add `build_field_analysis_graph` without changing existing coordinator graph behavior. Add `/workflows/crop-planning/field-analysis` to `main.py`.

**Step 5: Run tests**

Run: `cd ai-service; pytest -q`
Expected: PASS.

### Task 2: Backend DTOs, AI Client, and FieldAnalysis Runner

**Files:**
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/CropPlanningWorkflowDtos.cs`
- Modify: `backend/AgriAssist.Api/ExternalServices/AgenticAI/IAgenticAIClient.cs`
- Modify: `backend/AgriAssist.Api/ExternalServices/AgenticAI/AgenticAIClient.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/ICropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/CropPlanning/CropPlansWorkflowController.cs`
- Test: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`

**Step 1: Write failing backend workflow tests**

Add tests for running the pending FieldAnalysis step, persisting output, marking tool/validation state, safe failure on invalid AI output, and emitting Member 3 handoff JSON.

Run: `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj --filter CropPlanningAiWorkflowTests`
Expected: FAIL because FieldAnalysis runner does not exist.

**Step 2: Add DTOs and AI client method**

Add `FieldAnalysisInput`, `FieldAnalysisOutput`, `FieldConditionResponse`, `FieldAnalysisOpenIssueResponse`, and `Member3HandoffResponse`. Add `RunFieldAnalysisAsync`.

**Step 3: Add orchestration method**

Add `RunFieldAnalysisAsync(Guid cropPlanRequestId, CancellationToken)` to find the latest workflow, load pending `FieldAnalysis` step, build input from persisted crop plan/workflow context, call AI, validate evidence IDs, persist output, and move `CurrentStep` to `WeatherResourceAgent` when successful.

**Step 4: Add API endpoints**

Add `POST /api/crop-plans/{id}/run-field-analysis`, `GET /api/crop-plans/{id}/field-analysis-result`, and `GET /api/crop-plans/{id}/member-3-handoff`.

**Step 5: Run tests**

Run: `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj --filter CropPlanningAiWorkflowTests`
Expected: PASS.

### Task 3: Backend Internal Inspection Tools

**Files:**
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/InternalAgentToolDtos.cs`
- Modify: `backend/AgriAssist.Api/Controllers/Internal/InternalAgentToolsController.cs`
- Test: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`

**Step 1: Write failing tests**

Verify `GetRecentInspections`, `GetOpenCropIssues`, and `GetInspectionImageMetadata` are scoped to workflow field context and record `AgentToolExecution` against the FieldAnalysis step.

Run: `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj --filter AgentTool`
Expected: FAIL before tool endpoints exist.

**Step 2: Add DTOs**

Add compact inspection, observation, issue, follow-up, and image metadata records for internal AI tools.

**Step 3: Add internal endpoints**

Add endpoints under `/api/internal/agent-tools`, reuse token validation, enforce workflow/field scope, and resolve tool execution step by `agentStepId` or `CropFieldAnalysisAgent` when possible.

**Step 4: Run tests**

Run: `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj`
Expected: PASS.

### Task 4: Backend Inspection API Completion

**Files:**
- Modify: `backend/AgriAssist.Api/Dtos/Inspections/InspectionDtos.cs`
- Modify: `backend/AgriAssist.Api/Validators/Inspections/InspectionValidators.cs`
- Modify: `backend/AgriAssist.Api/Services/Inspections/IInspectionService.cs`
- Modify: `backend/AgriAssist.Api/Services/Inspections/InspectionService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/Inspections/InspectionsController.cs`
- Test: `backend/AgriAssist.Api.Tests/InspectionWorkflowTests.cs`
- Test: `backend/AgriAssist.Api.Tests/ValidatorTests.cs`

**Step 1: Write failing inspection tests**

Cover FieldOfficer authorization, search filters, detail/history retrieval, submit/close transitions, issue severity/status update, serious issue escalation, Cloudinary failure, image metadata listing, and follow-up completion.

Run: `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj --filter Inspection`
Expected: FAIL before APIs exist.

**Step 2: Add DTOs and validators**

Add query records, detail responses, status request, issue status request, image metadata response, and follow-up update request.

**Step 3: Implement service operations**

Keep business rules in `InspectionService`; add scoped reads and role checks. Treat `Completed` as submitted, `Closed` issue/follow-up states as final, and maintain Cloudinary upload through `ICloudinaryService`.

**Step 4: Add controller routes**

Add details, history, image metadata, issue details/status, follow-up list/update, submit, and close endpoints.

**Step 5: Run tests**

Run: `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj`
Expected: PASS.

### Task 5: React Inspection Views

**Files:**
- Modify: `frontend/react-app/src/types.ts`
- Modify: `frontend/react-app/src/App.tsx`
- Modify: `frontend/react-app/src/routing.tsx`
- Modify: `frontend/react-app/src/pages/InspectionsPage.tsx`
- Create: `frontend/react-app/src/pages/InspectionDetails.tsx`
- Create: `frontend/react-app/src/pages/InspectionsDashboard.tsx`
- Create: `frontend/react-app/src/pages/CropIssues.tsx`
- Create: `frontend/react-app/src/pages/CropIssueDetails.tsx`
- Create: `frontend/react-app/src/pages/EscalatedIssues.tsx`
- Create: `frontend/react-app/src/pages/FollowUpRecommendations.tsx`
- Create: `frontend/react-app/src/pages/InspectionHistory.tsx`
- Test: `frontend/react-app/src/pages/InspectionsPage.test.tsx`

**Step 1: Write failing React tests**

Cover protected inspection route, status/filter UI, detail navigation, escalation action, and follow-up completion state.

Run: `cd frontend\react-app; npm test -- InspectionsPage.test.tsx`
Expected: FAIL before views exist.

**Step 2: Add types and API calls**

Extend inspection, issue, follow-up, image metadata, history, and field-analysis types. Reuse the existing `api` client and page-level state.

**Step 3: Build views**

Use compact operational layouts, existing UI components, lucide icons, no new state library, and no marketing-style sections. Keep routes protected for Admin, FieldOfficer, and AgriculturalOfficer.

**Step 4: Run frontend checks**

Run: `cd frontend\react-app; npm test; npm run build`
Expected: PASS.

### Task 6: Flutter FieldOfficer Workflow

**Files:**
- Modify: `mobile/flutter_app/lib/models/api_models.dart`
- Modify: `mobile/flutter_app/lib/services/api_client.dart`
- Modify: `mobile/flutter_app/lib/state/app_state.dart`
- Modify: `mobile/flutter_app/lib/screens/login_screen.dart`
- Modify: `mobile/flutter_app/lib/screens/home_shell.dart`
- Modify: `mobile/flutter_app/lib/screens/inspection_screen.dart`
- Test: `mobile/flutter_app/test/inspection_screen_test.dart`
- Test: `mobile/flutter_app/test/widget_test.dart`

**Step 1: Write failing Flutter tests**

Cover role login defaults, field selection, camera/GPS state, form validation, submit error, history render, and upload action using a fake API client/state.

Run: `cd mobile\flutter_app; flutter test`
Expected: FAIL before workflow exists.

**Step 2: Add API/models**

Add inspection creation, observation creation, crop issue creation, image upload, history, follow-up, and detail models. Keep multipart upload through ASP.NET.

**Step 3: Build workflow**

Use Provider state: select field, start inspection, capture GPS, pick/capture image, add observations, report issue severity, submit inspection, upload image, and show history/follow-up state.

**Step 4: Run Flutter checks**

Run: `cd mobile\flutter_app; flutter analyze; flutter test`
Expected: PASS.

### Task 7: End-to-End Verification Docs

**Files:**
- Modify: `docs/testing/verification-log.md`
- Modify: `docs/ai-usage/member-2-crop-planning-contract.md`

**Step 1: Document handoff JSON**

Add the exact Member 3 handoff shape emitted by `GET /api/crop-plans/{id}/member-3-handoff`.

**Step 2: Document verification commands**

Record backend, AI-service, React, and Flutter commands and any skipped checks.

**Step 3: Final verification**

Run:
- `cd ai-service; pytest -q`
- `dotnet test backend\AgriAssist.Api.Tests\AgriAssist.Api.Tests.csproj`
- `cd frontend\react-app; npm test; npm run build`
- `cd mobile\flutter_app; flutter analyze; flutter test`

Expected: PASS or explicitly documented environment blocker.
