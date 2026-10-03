# Member 4 Single-Tool ReAct Evidence Retrieval Design

**Status:** Revised draft for review  
**Date:** 2026-10-03  
**Owner:** Member 4  
**Target:** AgriAssist AI service

## Purpose

Let Member 4's Scheduling Validation Agent retrieve one missing verified crop-profile record through a real model tool call. Keep the feature small enough to explain clearly: the model can ask for the evidence; ordinary validated code checks it; the existing deterministic scheduler creates the proposal; an authorized officer decides whether to approve it.

## What already exists

- SchedulingValidationAgent uses EvidenceScheduler to create candidate tasks, irrigation entries, and candidate reservations deterministically.
- The scheduling graph validates evidence before candidate generation and blocks when required evidence is missing.
- SchedulingValidationInput carries the workflow, candidate revision, Member 1–3 outputs, crop-profile evidence, and source metadata.
- InternalAgentToolsController exposes read-only GET routes for crop-plan context and verified crop reference profiles. Python already has typed crop-planning wrappers for those routes.
- Current AI-service provider configuration supports OpenAI. The provider interface supports JSON generation but does not yet expose function/tool calls.

## One recommended flow

1. **Check prerequisites in code.** Before any model call, require successful Member 1, 2, and 3 outputs for this workflow and the existing evidence bundle's upstream step IDs. Missing or failed upstream analysis is not refreshable.
2. **Keep the fast path.** If the verified crop profile and its stages are already present and consistent, skip the model and all retrieval HTTP calls. Run the current scheduler unchanged.
3. **Allow one model-selected action.** If the evidence bundle exists but its verified crop-profile identifier or stages are missing, give the OpenAI model exactly one tool: GetVerifiedCropProfile. Its schema has no caller-controlled identifiers. Trusted code supplies the crop-plan request and workflow IDs.
4. **Read existing sources.** The Python tool wrapper uses existing GET operations to retrieve crop-plan context and then the matching verified crop reference profile. No API route or write operation is added.
5. **Validate deterministically.** Check the typed response, crop type, workflow, profile status, source/version, verification timestamp, and presence of verified stages. If Member 3's persisted requirement source names a profile, the retrieved profile must match it.
6. **Schedule using fixed logic.** Merge only the validated profile evidence into a copy of the request, then run the existing EvidenceScheduler. Ignore any free-text model suggestion; the model never chooses dates, durations, quantities, or approval outcomes.
7. **Fail closed.** A refusal, invalid tool request, unavailable tool/provider, timeout, missing or mismatched profile, or invalid provenance keeps the original request and produces the existing safe MissingDependency result.

The model's one tool action and its observation are the ReAct contribution. The tool budget is exactly one call; there is no autonomous multi-tool loop. Complete evidence stays on the no-model path.

    validate prerequisites
       ├─ missing Member 1–3 result or step IDs → safe block
       ├─ verified profile complete → deterministic scheduler
       └─ profile identifier/stages missing → one model tool call
                                              ↓
                                     existing read-only GETs
                                              ↓
                                   validate source/provenance
                                       ├─ valid → scheduler
                                       └─ invalid/error → safe block

## Tool boundary

Expose one model-visible function, GetVerifiedCropProfile, with an empty argument object. Its implementation binds all IDs from the validated scheduling request and uses the existing CropPlanningTools wrapper. It may internally make the existing crop-plan-context GET followed by the crop-reference-profile GET, but the model can request this action only once.

Reject extra arguments, unknown tool names, model-supplied workflow or entity IDs, arbitrary URLs, and unexpected provider response shapes. Accept only an active, available, verified profile with matching crop context and at least one verified stage. A profile verified in the future or with a mismatched entity, source version, or Member 3 profile reference is stale/invalid. Do not invent a time-to-live.

An empty verified irrigation-rule list remains valid: the deterministic scheduler may return zero irrigation entries with its existing warning. This retrieval feature must not invent irrigation rules or durations.

## Safety and approval

- No model action can create or update farm records. Retrieval uses existing GET routes only.
- Member 1–3 statuses, warnings, conclusions, and provenance are never synthesized or overwritten.
- EvidenceScheduler remains the sole source of scheduling decisions and candidate content.
- Candidates remain proposals. Only the existing authorized AgriculturalOfficer/Admin workflow may approve them.
- Approval-time inventory and scheduling rechecks, workflow IDs, candidate revisions, concurrency controls, audit history, and rollback behavior remain unchanged.
- Do not log prompts, credentials, complete farm records, or raw tool payloads. Record only sanitized workflow/step/revision IDs, tool name, source ID/version, result class, and elapsed time.

## Rollout

Add one disabled-by-default setting, SCHEDULING_PROFILE_RETRIEVAL_ENABLED=false. Use the existing provider and backend-tool timeouts with a single overall 45-second retrieval deadline. Reject observations larger than 16 KiB. Enable the feature only in a controlled demo/development environment after tests pass. Keep the normal deterministic fast path and safe block as the default behavior.

## Acceptance checks

1. Complete valid evidence produces the same deterministic result with zero provider and retrieval calls.
2. Missing profile metadata can invoke only GetVerifiedCropProfile once, with IDs bound by trusted code.
3. A matching verified profile fills only the missing profile evidence and allows the existing deterministic scheduler to run.
4. Missing upstream outputs, missing step IDs, malformed tool calls, extra arguments, provider refusal/failure, timeout, missing backend token, oversized response, wrong workflow/crop/profile/source version, or invalid verification time produce no candidate from partial evidence.
5. No tool write or approval occurs; candidate revision and workflow ID stay unchanged.
6. Empty verified irrigation rules remain valid and produce no invented irrigation entries.
7. Existing approval, stale-revision, authorization, inventory recheck, conflict, rollback, and concurrent-approval tests continue to pass.
8. Logs contain no prompt, credential, or full tool payload.

## Out of scope

- Stock, reservation, weather, field, or inspection retrieval.
- Multiple model-selected tools, multi-round planning, or autonomous retries.
- Any model-generated schedule, risk score, resource amount, database write, task creation, reservation, or approval.
- New ASP.NET endpoints or changes to Member 1–3 analysis.
