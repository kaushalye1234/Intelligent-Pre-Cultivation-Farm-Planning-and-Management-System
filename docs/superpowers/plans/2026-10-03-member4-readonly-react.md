# Member 4 Single-Tool ReAct Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the scheduling agent make one safe read-only request for missing verified crop-profile evidence, then let existing deterministic code build the proposal.

**Architecture:** Keep the current scheduler and approval workflow unchanged. If upstream analysis is complete but crop-profile evidence is incomplete, the OpenAI model may call exactly one fixed tool, GetVerifiedCropProfile, with no model-controlled IDs. Typed existing GET wrappers retrieve the matching profile; deterministic code validates it and passes it to the current scheduler or returns MissingDependency.

**Tech Stack:** Python, FastAPI, Pydantic, LangGraph, the existing OpenAI SDK and BackendToolClient.

**Spec:** `docs/superpowers/specs/2026-10-03-member4-readonly-react-design.md`

## Global Constraints

- Keep the feature disabled by default behind `SCHEDULING_PROFILE_RETRIEVAL_ENABLED=false`.
- A complete evidence request must call neither the model nor the profile tool.
- Missing Member 1, 2, or 3 output or required step IDs must block before any model call.
- Expose exactly one model-visible read-only tool and allow exactly one tool call per request.
- The tool accepts no caller-controlled identifiers; trusted request context binds workflow, crop plan, crop type, and variety.
- Use only existing crop-plan-context and crop-reference-profile GET routes through typed Python wrappers.
- Never synthesize or overwrite Member 1–3 statuses, conclusions, warnings, or source provenance.
- EvidenceScheduler remains the sole source of candidate tasks, irrigation entries, resource reservations, dates, and risk assessment.
- No farm writes or approvals occur during retrieval. The existing officer approval transaction remains unchanged.
- Preserve workflow ID and candidate revision, audit behavior, and safe failure handling.

## Review Focus

- **Complete profile present:** prove zero provider/tool calls and preserve the existing deterministic output in Task 3.
- **Upstream output or step missing:** prove no retrieval is attempted and result remains MissingDependency in Task 2.
- **Unknown, multiple, or malformed model tool call:** reject without dispatching a request in Tasks 1 and 2.
- **Wrong crop/profile/source or unverified profile:** reject evidence and create no candidate in Task 2.
- **Provider/backend timeout, missing token, or oversized response:** stop safely and preserve the original request in Tasks 2 and 3.

## File Map

| File | Responsibility |
| --- | --- |
| Modify: `ai-service/providers/base_llm_provider.py` | Add one typed tool-call result/method without changing existing JSON generation. |
| Modify: `ai-service/providers/openai_provider.py` | Implement strict single-function calling for the configured provider. |
| Modify: `ai-service/config.py` | Add the disabled-by-default feature switch. |
| Create: `ai-service/tools/scheduling_evidence_tools.py` | Expose only GetVerifiedCropProfile and bind trusted identifiers to existing GET wrappers. |
| Create: `ai-service/agents/scheduling_profile_retriever.py` | Check prerequisites, request/validate one profile, and return the original input on safe failure. |
| Modify: `ai-service/agents/scheduling_validation_agent.py` | Invoke the optional retriever before existing deterministic checks. |
| Modify: `ai-service/graph/workflow_graph.py` | Add one conditional retrieval step before existing evidence validation. |
| Modify: `ai-service/main.py` | Construct the provider and read-only wrappers only when the feature is enabled. |
| Modify: `ai-service/tests/test_llm_providers.py` | Cover the single typed OpenAI tool-call response. |
| Create: `ai-service/tests/test_scheduling_evidence_tools.py` | Cover the fixed tool, trusted IDs, typed GET use, and profile validation. |
| Modify: `ai-service/tests/test_scheduling_validation_agent.py` | Cover prerequisites, one-call limit, safe failures, and unchanged deterministic results. |
| Modify: `ai-service/tests/test_scheduling_validation_graph.py` | Cover fast path, retrieval path, and safe-block path. |
| Modify: `ai-service/tests/test_scheduling_validation_api.py` | Confirm feature-flag wiring leaves route/auth/response contracts unchanged. |

No backend, React, Flutter, migration, or database changes are expected. If existing GET wrappers cannot retrieve and validate the profile needed by the approved spec, stop and request a design update; do not add a new endpoint.

---

### Task 1: Add a typed single-tool response to the OpenAI provider

**Files:**
- Modify: `ai-service/providers/base_llm_provider.py`
- Modify: `ai-service/providers/openai_provider.py`
- Modify: `ai-service/tests/test_llm_providers.py`

**Interfaces:**
- Add a typed result for either a terminal response or exactly one function call with call ID, function name, and JSON arguments.
- Add `async def generate_tool_call(self, prompt: str, tool_schema: dict[str, object]) -> ToolCallResult` to BaseLLMProvider. The default raises the existing safe provider-configuration error.
- OpenAIProvider requests the supplied strict function schema and parses only the expected function name and valid JSON object. It never executes a tool. Existing `generate_json` behavior remains unchanged.

- [ ] **Step 1: Write failing tests** `test_openai_provider_returns_one_typed_tool_call`, `test_openai_provider_returns_terminal_response`, `test_provider_rejects_unknown_or_multiple_tool_calls`, and `test_provider_rejects_malformed_tool_arguments`.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest tests/test_llm_providers.py -q`; confirm the new tests fail before implementation.
- [ ] **Step 3: Implement** the minimal typed provider method using the installed OpenAI SDK interface and existing timeout/error handling.
- [ ] **Step 4: Rerun** `cd ai-service; python -m pytest tests/test_llm_providers.py -q`; expect all provider tests to pass.

### Task 2: Implement the one verified crop-profile tool

**Files:**
- Create: `ai-service/tools/scheduling_evidence_tools.py`
- Create: `ai-service/agents/scheduling_profile_retriever.py`
- Modify: `ai-service/tests/test_scheduling_evidence_tools.py`
- Modify: `ai-service/tests/test_scheduling_validation_agent.py`

**Interfaces:**
- Expose exactly `GetVerifiedCropProfile` with an empty argument object. Any extra argument is rejected.
- The tool uses the trusted request's crop-plan request ID and workflow ID to call existing `CropPlanningTools.get_crop_plan_context`, then `get_crop_reference_profile` with the returned crop type and variety.
- Implement `async def retrieve_profile(request: SchedulingValidationInput, provider: BaseLLMProvider, tools: SchedulingEvidenceTools) -> SchedulingValidationInput`. Return a copy with only the verified crop-profile evidence filled when all checks pass; return the original request if no retrieval is allowed or any step fails.
- Retrieval is eligible only when Member 1 status is Planned, Member 2 and Member 3 statuses are Analyzed for the same workflow, required upstream step IDs exist, and the existing evidence bundle is present but lacks its profile ID or verified stages.
- Validate profile availability, workflow/crop/variety match, Member 3's profile reference when present, source/version metadata, `verifiedAt <= now in UTC`, and at least one verified stage. No age-based TTL is added.
- Cap this path at one model call, one model tool call, existing provider/backend timeouts, one 45-second overall deadline, and 16 KiB per serialized observation. Never accept a model-supplied ID or free-text scheduling advice.

- [ ] **Step 1: Write failing tests** `test_missing_upstream_skips_provider`, `test_complete_profile_skips_provider`, `test_empty_tool_args_use_trusted_request_ids`, `test_profile_request_uses_existing_get_wrappers`, `test_matching_profile_fills_only_missing_evidence`, `test_wrong_crop_or_member3_profile_blocks`, `test_future_or_unverified_profile_blocks`, `test_unknown_extra_or_multiple_call_blocks`, `test_timeout_token_error_or_oversize_returns_original_request`, and `test_no_model_text_changes_schedule`.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest tests/test_scheduling_evidence_tools.py tests/test_scheduling_validation_agent.py -q`; confirm the new tests fail.
- [ ] **Step 3: Implement** the fixed one-tool wrapper and deterministic eligibility/response validation. Keep all failures proposal-free by returning the unchanged request for existing dependency validation to block.
- [ ] **Step 4: Rerun** the focused command; expect matching verified profiles to pass and all unsafe cases to stay blocked.

### Task 3: Wire the feature flag and one retrieval node

**Files:**
- Modify: `ai-service/config.py`
- Modify: `ai-service/agents/scheduling_validation_agent.py`
- Modify: `ai-service/graph/workflow_graph.py`
- Modify: `ai-service/main.py`
- Modify: `ai-service/tests/test_scheduling_validation_graph.py`
- Modify: `ai-service/tests/test_scheduling_validation_api.py`

**Interfaces:**
- Add `scheduling_profile_retrieval_enabled: bool = False` with alias `SCHEDULING_PROFILE_RETRIEVAL_ENABLED`.
- Keep `SchedulingValidationAgent()` valid for all existing callers. Inject an optional profile retriever; when absent or disabled, retain today's deterministic path.
- Add one LangGraph node before existing `validate_evidence`; it either supplies validated profile evidence or leaves the original request untouched. The existing validation, candidate, and risk nodes remain unchanged.
- In `main.py`, construct OpenAI provider and existing read-only tool clients only when the flag is true. Keep endpoint path, auth dependency, and response model unchanged.

- [ ] **Step 1: Write failing tests** `test_flag_defaults_to_disabled`, `test_disabled_feature_constructs_no_provider_or_tool_client`, `test_graph_skips_retrieval_for_complete_evidence`, `test_graph_retrieves_profile_before_candidate`, `test_graph_safe_failure_returns_missing_dependency_without_candidate`, and `test_scheduling_api_contract_is_unchanged`.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest tests/test_scheduling_validation_graph.py tests/test_scheduling_validation_api.py -q`; confirm the new tests fail.
- [ ] **Step 3: Implement** only the conditional node and dependency injection. Do not alter deterministic candidate logic, backend contracts, or approval behavior.
- [ ] **Step 4: Rerun** the focused graph/API/scheduler tests; expect existing deterministic fixture outputs to remain unchanged.

### Task 4: Verify the AI-service change

- [ ] **Step 1: Run** `cd ai-service; python -m compileall -q .`; require exit code 0.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest`; require all AI-service tests to pass.
- [ ] **Step 3: Review** `git diff --check`, conflict markers, and changed-file list. Confirm no write-capable tool, new backend endpoint, secret, or unrelated dependency was added.
- [ ] **Step 4: Confirm** the complete-evidence output matches the existing deterministic behavior; report backend, React, Flutter, and PostgreSQL checks as not run because this plan changes only the AI service.
- [ ] **Step 5: Commit** the focused AI-service implementation on a clean feature branch from current dev. Merge through a reviewed PR only after applicable CI passes; do not push directly to dev/main.

## Phase Boundary

Phases 1–3 remain complete for the merged baseline. This is a small Member 4 follow-up enhancement, not a rewrite of scheduling/approval and not a prerequisite for the separate deployment and final-report deliverables unless the group chooses to include it in the demo.
