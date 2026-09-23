# Farmer Mobile and Staff Field-Analysis Separation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make Flutter Farmer-only and add an exact crop-plan-linked pre-planting Field Officer assessment to the existing React/ASP.NET/AI workflow.

**Architecture:** Extend `FieldInspection` with an optional crop-plan link and purpose, store the structured assessment as typed observations, and scope existing AI evidence tools to that inspection. Keep Farmer crop planning/status in Flutter and operational work in React.

**Tech Stack:** Flutter/Dart, React/TypeScript/Vite, ASP.NET Core 8/EF Core/PostgreSQL, FastAPI/Pydantic, xUnit, Vitest, pytest.

---

### Task 1: Lock Flutter to Farmer-only navigation and sessions

**Files:**
- Modify: `mobile/flutter_app/lib/main.dart`
- Modify: `mobile/flutter_app/lib/screens/home_shell.dart`
- Modify: `mobile/flutter_app/lib/state/app_state.dart`
- Modify: `mobile/flutter_app/lib/services/api_client.dart`
- Modify: `mobile/flutter_app/lib/models/api_models.dart`
- Modify: `mobile/flutter_app/pubspec.yaml`
- Delete: `mobile/flutter_app/lib/screens/inspection_screen.dart`
- Delete: `mobile/flutter_app/lib/screens/resources_screen.dart`
- Delete: `mobile/flutter_app/test/inspection_screen_test.dart`
- Test: `mobile/flutter_app/test/auth_navigation_test.dart`

1. Add failing widget tests for the three Farmer destinations and the staff-portal message.
2. Run the targeted test and confirm failure.
3. Remove staff destinations, screens, operational state/API/models, and unused device dependencies.
4. Reject and clear non-Farmer login/restored sessions before authenticated data loading; retain an `AuthGate` defense.
5. Run targeted Flutter tests and confirm pass.

### Task 2: Add the linked pre-planting inspection model and migration

**Files:**
- Modify: `backend/AgriAssist.Api/Models/Inspections/FieldInspection.cs`
- Modify: `backend/AgriAssist.Api/Data/AppDbContext.cs`
- Create: `backend/AgriAssist.Api/Migrations/<timestamp>_LinkPrePlantingInspectionToCropPlan.cs`
- Create: `backend/AgriAssist.Api/Migrations/<timestamp>_LinkPrePlantingInspectionToCropPlan.Designer.cs`
- Modify: `backend/AgriAssist.Api/Migrations/AppDbContextModelSnapshot.cs`
- Test: `backend/AgriAssist.Api.Tests/InspectionWorkflowTests.cs`

1. Add failing tests for null legacy links, the `Routine` default, the crop-plan FK, and uniqueness.
2. Add the model properties and enum.
3. Configure the optional restricted FK and filtered composite unique index.
4. Generate migration, designer, and snapshot together.
5. Run targeted backend tests.

### Task 3: Add the Field Officer assessment API

**Files:**
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/CropPlanningWorkflowDtos.cs`
- Modify: `backend/AgriAssist.Api/Validators/CropPlanning/CropPlanningValidators.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/ICropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/CropPlanning/CropPlansWorkflowController.cs`
- Modify: `backend/AgriAssist.Api/Program.cs`
- Test: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`
- Test: `backend/AgriAssist.Api.Tests/ValidatorTests.cs`

1. Add failing tests for Field Officer upsert, exact plan/field linkage, observation replacement, view access, forbidden writes by other roles, and validation.
2. Add typed request/response records for the eight approved pre-planting fields and image metadata.
3. Add validator rules without post-planting fields.
4. Implement idempotent draft upsert and typed response reconstruction.
5. Add staff-view GET and Field Officer-only PUT endpoints.
6. Run targeted backend tests.

### Task 4: Scope field-analysis evidence to the exact assessment

**Files:**
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/CropPlanningWorkflowDtos.cs`
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/Internal/InternalAgentToolsController.cs`
- Modify: `ai-service/schemas/field_analysis.py`
- Modify: `ai-service/tools/inspection_tools.py`
- Modify: `ai-service/agents/crop_field_analysis_agent.py`
- Test: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`
- Test: `ai-service/tests/test_crop_field_analysis_agent.py`

1. Add failing tests for missing/unsubmitted assessment rejection and exclusion of unrelated inspections.
2. Synchronize `cropPlanRequestId` and `prePlantingInspectionId` in C# and Python input contracts.
3. Require a completed linked assessment before starting the agent step.
4. Scope inspection, issue, and image evidence to that assessment while preserving workflow/field checks.
5. Tighten output validation to the linked evidence ID.
6. Run targeted backend and AI tests.

### Task 5: Add the React Field Officer assessment panel

**Files:**
- Create: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`
- Create: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.css`
- Modify: `frontend/react-app/src/pages/WorkflowReviewPage.tsx`
- Modify: `frontend/react-app/src/pages/WorkflowReviewPage.test.tsx`
- Modify: `frontend/react-app/src/types.ts`

1. Add failing tests for Field Officer editing, Agricultural Officer/Admin read-only behavior, required fields, ordered save/upload/submit/agent calls, stopped chains on error, and result refresh.
2. Add assessment/result TypeScript contracts.
3. Build the accessible page-local form with only the approved fields and evidence picker.
4. Implement load/save, existing image upload, existing inspection submission, field-analysis execution, and review reload.
5. Disable duplicate submission and render actionable states.
6. Run targeted Vitest tests.

### Task 6: Verify all projects and commit locally

**Files:**
- Modify: `docs/ai-usage/member-2-crop-planning-contract.md`

1. Document the linked assessment and synchronized input fields.
2. Run required backend restore, Release build, and complete tests.
3. Run AI compile and pytest.
4. Run React clean install, lint, production build, and complete tests.
5. Run Flutter dependency restore, analyze, and complete tests when installed.
6. Run `git diff --check`, scan tracked source for conflict markers, confirm the branch, and review scope.
7. Create focused local commits only after verification; show local history and final status without pushing.
