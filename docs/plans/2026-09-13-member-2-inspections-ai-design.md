# Member 2 Inspections AI Design

Date: 2026-09-13
Status: Approved

## Context

Member 1 already added the shared FastAPI/LangGraph AI service, provider abstraction, backend tool client, crop planning coordinator, and AgentWorkflow persistence. Member 2 extends that foundation with inspection evidence analysis and field officer inspection workflows. This design does not recreate `main.py`, providers, config, auth, or the shared graph foundation.

## Architecture

ASP.NET remains the only public application API. React and Flutter continue to call ASP.NET endpoints only. The Python AI service exposes a narrow FieldAnalysis endpoint that uses allow-listed backend tools to fetch stored field, crop cycle, inspection, crop issue, image metadata, and crop reference evidence.

Member 2 output is persisted in the existing `AgentStep.OutputJson` for the `FieldAnalysis` step. Tool calls are recorded in `AgentToolExecution`. No shadow table is introduced for field-analysis results.

## Agent Behavior

`CropFieldAnalysisAgent` analyzes stored context only. It summarizes field condition, open issues, evidence-linked priority, warnings, and whether human review is needed. It must not invent observations, issue IDs, diagnoses, chemical treatments, approvals, or mutations.

The approved image rule is metadata-only. Inspection images upload through ASP.NET and Cloudinary, but images are not sent to the LLM. The agent may see image metadata such as image ID, inspection ID, URL, content type, size, and upload time so humans know evidence exists.

## Backend Data Flow

After Member 1 completes, the pending `AgentStep` has `agentName = CropFieldAnalysisAgent` and `stepName = FieldAnalysis`. Running Member 2 builds structured input from the persisted workflow, including `workflowId`, `fieldId`, `cropCycleId`, `requestedAnalysis`, and `cropReferenceProfileId` when available.

ASP.NET calls the AI service, validates the field-analysis envelope, stores output in the existing FieldAnalysis `AgentStep`, records validation results, and advances the workflow to Member 3 when safe. Member 3 consumes only the persisted Member 2 output and verified field/location/date/priority context.

## Inspection Operations

The existing inspection entities remain the source of truth: `FieldInspection`, `InspectionObservation`, `CropIssue`, `InspectionImage`, and `FollowUpRecommendation`. The API is completed with detail, history, richer search/filter/sort/pagination, submit/close transitions, issue severity/status transitions, serious issue escalation, image metadata listing, and follow-up completion state.

FieldOfficer, AgriculturalOfficer, and Admin may manage inspections. Farmers retain read access only to records scoped to their farms.

## Client UX

Flutter implements a FieldOfficer workflow: role-aware login, field selection, inspection start, GPS capture, camera/image picker, observations, crop issue severity, submission, history, and follow-up state. Uploads are multipart to ASP.NET and never expose Cloudinary credentials.

React keeps the existing API client and protected-route model while refining inspection views into dashboard, inspection list/detail, crop issue list/detail, escalated issues, follow-up recommendations, and history surfaces.

## Testing

Backend tests cover validators, FieldOfficer authorization, inspection status transitions, severity/escalation, Cloudinary failure, image metadata, field-analysis persistence, and Member 3 handoff JSON. Python tests cover golden analysis, missing inspection data, evidence ID preservation, no invented issue IDs, malformed output, prompt injection in officer notes, provider timeout, and safe failure. React tests cover protected routes, filters/status, and escalation. Flutter tests cover form validation, camera/GPS state, navigation, submit/error, and history rendering.
