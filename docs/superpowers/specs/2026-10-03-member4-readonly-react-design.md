# Member 4 Read-Only ReAct Evidence Retrieval Design

**Status:** Draft for review  
**Date:** 2026-10-03  
**Owner:** Member 4  
**Target:** AgriAssist AI service and internal agent-tool API

## 1. Purpose

Add a narrowly scoped ReAct (reasoning and acting) step to Member 4's Scheduling Validation Agent. The step may select from a fixed set of read-only tools to retrieve or refresh persisted evidence needed by the deterministic scheduler. It must not make scheduling decisions or change farm data.

This is an enhancement to the existing proposal workflow. It does not replace the deterministic EvidenceScheduler, upstream Member 1–3 agents, ASP.NET authorization/business rules, or officer approval.

## 2. Current verified design

On the current dev branch:

- SchedulingValidationAgent delegates to EvidenceScheduler, which checks persisted inputs and deterministically creates candidate tasks, irrigation entries, and candidate reservations. It does not write farm data.
- The LangGraph scheduling workflow currently follows validate evidence → build candidate → assess risk → end. Invalid or incomplete dependencies stop at validation.
- SchedulingValidationInput already carries the workflow ID, candidate revision, Member 1–3 outputs, evidence provenance, and snapshots of existing tasks and irrigation.
- InternalAgentToolsController exposes GET-only routes under /api/internal/agent-tools. Relevant existing routes include crop-plan-context, fields, crop-reference-profiles, crop-resource-requirements, resource-availability, existing-reservations, low-stock-status, and weather-forecast. Each is wrapped in the internal tool response and workflow scope checks.
- Python backend clients and typed wrappers already exist for several of those read routes.
- The generic LLM provider contract supports JSON generation but does not currently define a provider-neutral tool-call response. A typed tool-call contract and allowlisted dispatcher are therefore required before a genuine model-selected ReAct loop can be implemented.
- The current scheduling graph does not call those backend tools or an LLM. It must keep that fast, deterministic path when the supplied evidence is complete.

## 3. Goals and non-goals

### Goals

1. Recover eligible, persisted evidence when the scheduling request is missing or explicitly stale evidence that can be refreshed safely.
2. Make model-selected actions visible, bounded, typed, and auditable.
3. Ensure the refreshed evidence is checked for workflow, entity, provenance, and revision consistency before scheduling uses it.
4. Preserve current candidate-only behavior and human approval requirements.
5. Fail safely when tools, model calls, or evidence validation fail.

### Non-goals

- Let the model select candidate dates, task content, irrigation quantities or durations, resource quantities, weather risk, or schedule ordering.
- Let the model generate or approve the final candidate.
- Create farm tasks, irrigation schedules, inventory reservations, or other persistent records during retrieval.
- Expose arbitrary HTTP, database, filesystem, shell, or general web-search tools to the model.
- Re-run Member 1–3 agents or synthesize missing upstream results.
- Change the existing workflow or candidate revision protocol beyond what is needed to record the retrieval attempt and validate refreshed evidence.
- Add a provider other than one that implements the typed, constrained tool-call contract. Provider selection remains a later implementation decision.

## 4. Proposed flow

The ReAct evidence retrieval node sits before deterministic dependency validation. It is conditional: complete supplied evidence proceeds directly to validation without an LLM call.

1. **Inspect request deterministically.** Check required fields and the existing evidence bundle. Identify only refreshable evidence gaps. Never ask the model to decide whether a business rule is satisfied.
2. **Fast path.** If all required evidence and source metadata are present and match the request's workflow and candidate revision, skip ReAct and continue to existing deterministic validation.
3. **Bounded ReAct retrieval.** If a gap is refreshable, provide the model a minimal request summary and the fixed tool schemas. The model may request one of the allowlisted reads. The dispatcher validates tool name and arguments, binds workflow ID, candidate revision, and agent step ID from trusted graph state, then invokes the typed read wrapper. The model receives a minimized, typed observation.
4. **Validate refresh.** Deterministic code validates response schema, entity IDs, workflow scope, source/version/verified-at metadata, freshness policy, and candidate revision. Only accepted persisted evidence may update the in-memory request state.
5. **Deterministic scheduling.** Run the existing EvidenceScheduler against the validated request. Its current rules remain the sole source for candidate generation and risk assessment.
6. **Safe stop.** On an unrefreshable dependency, exhausted budget, refusal, timeout, tool error, malformed result, stale revision, or failed verification, use the existing MissingDependency or human-review/blocking result. Do not guess or fall through with partial evidence.

Suggested graph shape:

    inspect_inputs
      ├─ complete ───────────────> validate_evidence
      └─ refreshable_gap ────────> react_select_tool
                                      ↓
                                  execute_read
                                      ↓
                                  validate_observation
                                      ├─ more evidence needed and budget remains → react_select_tool
                                      ├─ evidence complete → validate_evidence
                                      └─ unsafe, stale, failed, or exhausted → safe blocked result
    validate_evidence → build_candidate → assess_risk → END

The graph should make the number of tool calls explicit in state and enforce a hard maximum in code. Tool observations and model messages are data, not executable instructions.

## 5. Tool allowlist and data limits

The initial allowlist should contain only existing read tools needed to resolve a known gap:

| Tool | Intended use | Required scope |
| --- | --- | --- |
| GetCropReferenceProfile | Refresh verified stage and irrigation-rule evidence | Crop type/profile constrained to the workflow |
| GetFieldDetails | Refresh field facts required to validate the selected field | Field ID must match the workflow request |
| GetCropResourceRequirements | Refresh Member 3 persisted requirement output | Crop-plan request and workflow must match |
| GetResourceAvailability | Refresh stock snapshot for resource IDs already present in verified requirements | IDs are derived from validated persisted requirements |
| GetExistingReservations | Refresh reservation snapshot for the same resource IDs | Workflow and resource IDs are fixed by trusted state |
| GetWeatherForecast | Refresh forecast evidence used by Member 3's persisted analysis | Workflow-bound; the model cannot supply location or external URL |

Additional Member 1 or 2 read tools must be separately justified and added to the schema, dispatcher, scope checks, and tests. The model may choose only a tool name and schema-defined arguments. It may not provide workflow ID, candidate revision, authorization token, arbitrary resource IDs, arbitrary path, or URL; trusted graph state supplies those values.

Do not automatically fetch an entire farm history or large inspection/image payload just because a model requests it. Retrieval should target the specific gap and use existing bounded typed responses. If no existing endpoint returns the exact data needed for a refresh, either keep the current safe block or propose a narrowly scoped read-only endpoint as a separate reviewed design change.

## 6. Contract and validation requirements

Implementation must introduce a typed provider-neutral tool-call result, or a clearly bounded provider adapter, with:

- a tool name from the fixed allowlist;
- arguments parsed into that tool's strict schema;
- a stable call ID and bounded text/JSON observation;
- an explicit terminal response when the model has no further tool calls.

The dispatcher must reject unknown tools, extra argument fields, invalid IDs, oversized arguments, repeated calls beyond budget, and tool results above a configured size limit. It must use the existing BackendToolClient and typed tool wrappers instead of constructing URLs or issuing raw HTTP from model output.

Before any refreshed result is accepted, deterministic validation must verify:

- response envelope indicates success and matches the expected schema;
- returned farm, field, crop plan, crop type, profile, resource, and workflow identifiers match trusted request context;
- source and verification metadata are present wherever required by the scheduling contract;
- verified-at/source-version policy is met; stale data is not silently treated as current;
- output belongs to the active workflow and candidate revision, or is explicitly treated as a current read snapshot under a documented policy;
- refreshed data does not overwrite a newer persisted upstream agent result;
- data stays within existing count and string-size limits.

A successful read is not by itself evidence that upstream analysis completed. ReAct must never synthesize Member 1–3 status, warnings, conclusions, or provenance.

## 7. Limits, errors, and fallback

Initial implementation defaults should be configurable and conservative: at most 3 model/tool rounds, at most 5 total tool calls, one overall retrieval deadline, and the existing backend tool timeout for each call. The concrete values and model token limits must be finalized against current configuration and tested. No unbounded loop or autonomous retry is allowed.

Safe fallback behavior:

- no configured tool-call-capable provider: skip retrieval and return the existing deterministic dependency result;
- provider refuses, emits malformed or unsupported call, or returns no valid answer: stop safely;
- timeout, cancellation, HTTP failure, missing token, or invalid envelope: stop safely;
- result is missing, stale, mismatched, oversized, or unverified: reject it and stop safely;
- call budget or total deadline is exhausted: stop safely;
- when evidence is still incomplete after the loop: return MissingDependency or the existing human-review/blocking state with a precise constraint.

Do not place credentials, full prompts, unnecessary personal data, or complete farm records in logs. Record sanitized tool name, workflow/step/revision IDs, source IDs and versions, result class, elapsed time, and stop reason using existing audit/logging facilities.

## 8. Trust and approval boundary

The AI service's backend tool token authenticates internal calls; it does not grant the model unrestricted authorization. Tool scope must be enforced again by ASP.NET for every request. Request ownership, workflow binding, and resource-to-workflow relationships remain backend responsibilities.

All retrieval is read-only. Candidate tasks, irrigation schedules, and reservations remain proposals. Only the existing authorized officer workflow can approve them. The approval transaction must continue to recheck inventory and scheduling constraints, reject stale candidate revisions, and preserve existing concurrency and rollback guarantees. The ReAct node cannot call approval or persistence endpoints because they are absent from the allowlist and the internal retrieval controller.

## 9. Acceptance criteria

The follow-up implementation is ready for review only when tests prove:

1. Complete evidence takes the no-model/no-tool fast path and produces the same deterministic candidate as before.
2. A known refreshable gap can be filled only by a matching persisted read response.
3. The agent can select only allowlisted tools; unknown tools and malformed/extra arguments are rejected without HTTP calls.
4. Workflow, entity, source, freshness, and revision mismatches are rejected and cannot reach candidate generation.
5. Tool/model timeout, failure, invalid JSON, refusal, missing token, and exhausted call budget end in safe blocked or human-review output.
6. The loop has deterministic hard limits and cannot repeat indefinitely.
7. Tool responses are read-only; no task, irrigation, reservation, or approval record is written.
8. Candidate scheduling still comes exclusively from EvidenceScheduler and remains deterministic for identical validated inputs.
9. Existing approval, stale-revision, authorization, inventory recheck, conflict, rollback, and concurrent-approval tests continue to pass.
10. Logs include enough sanitized provenance to trace retrieval without recording secrets or unnecessary personal data.
11. AI-service tests and the applicable backend, React, and Flutter CI jobs pass for changed projects.

## 10. Rollout

Implement behind a disabled-by-default feature setting. First ship the typed tool-call contract, strict dispatcher, validation, and unit tests. Then enable only for a provider with verified tool-call support and only for the allowlisted retrieval tools. Keep the deterministic fast path available as the default and fallback. Compare retrieval attempts, safe-stop reasons, latency, and deterministic scheduling outputs before enabling broadly.

## 11. Open implementation decisions

These are intentionally deferred until this spec is approved and the implementation plan is written:

- which configured LLM provider will supply constrained function/tool calls;
- exact freshness thresholds per evidence type;
- final retrieval deadline, call limits, and response byte/token caps;
- whether any exact missing field needs a new narrowly scoped internal GET endpoint;
- feature-setting name, default, and operational metrics.

No open decision may weaken the read-only allowlist, deterministic scheduler boundary, or explicit officer approval requirement.
