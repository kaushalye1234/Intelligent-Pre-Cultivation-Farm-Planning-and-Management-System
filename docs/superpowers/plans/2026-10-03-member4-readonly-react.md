# Member 4 Read-Only ReAct Evidence Retrieval Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a bounded, model-selected, read-only evidence retrieval loop before Member 4's existing deterministic scheduler, while preserving proposal-only output and officer approval.

**Architecture:** Keep complete and already-valid requests on the existing deterministic fast path. For a narrowly classified refreshable evidence gap, a typed provider tool-call contract drives a fixed dispatcher that uses existing GET wrappers; deterministic validators merge only matching persisted results into an in-memory request. EvidenceScheduler remains the sole candidate generator and risk assessor, and every unavailable or unsafe result exits through the existing safe dependency/blocking path.

**Tech Stack:** Python 3.11-compatible code, FastAPI, Pydantic 2, LangGraph, OpenAI SDK already pinned by ai-service/requirements.txt, httpx, pytest, pytest-asyncio.

**Spec:** docs/superpowers/specs/2026-10-03-member4-readonly-react-design.md

## Global Constraints

- Keep the backend tool surface GET-only; do not add a write endpoint for this feature.
- Keep every model action on a fixed allowlist with strict typed arguments; trusted graph state supplies workflow, step, and candidate revision context.
- Retrieval is disabled by default. When enabled, limits are at most 3 model turns, at most 5 total tool calls, one 45-second overall retrieval deadline, and at most 16 KiB of serialized observation per call.
- A complete evidence request must not call the model or backend retrieval tools.
- Never synthesize or replace Member 1–3 completion status, warnings, conclusions, or source provenance.
- Do not generate candidates from an unverified, mismatched, stale, malformed, timed-out, or partially accepted retrieval.
- EvidenceScheduler remains deterministic and read-only; no final task, irrigation schedule, inventory reservation, or approval is created before explicit officer approval.
- Preserve workflow ID, candidate revision, ownership scope, approval-time inventory/schedule rechecks, concurrency handling, rollback, and audit behavior.
- Use existing typed BackendToolClient wrappers; reject unknown tool names, extra arguments, arbitrary URLs/paths, and model-supplied trusted context.
- Run AI-service checks and the applicable repository checks for any shared file changed; do not claim PostgreSQL behavior from EF InMemory tests.

## Review Focus

- **Valid request with complete evidence:** ensure no model call or retrieval HTTP request occurs; pin in Task 1 and Task 5.
- **Missing whole upstream analysis versus refreshable supporting evidence:** block the former and refresh only the latter; pin in Task 1 and Task 4.
- **Cross-workflow/entity/profile/source mismatch or altered candidate revision:** reject it before it can change the scheduling request; pin in Task 3 and Task 4.
- **Provider returns malformed, unsupported, repeated, or over-budget calls:** stop safely without dispatching unapproved tools; pin in Task 2 and Task 4.
- **Timeout, cancellation, missing token, HTTP error, or oversized observation:** return the existing safe dependency result without scheduling from partial data; pin in Task 3 and Task 4.

---

## File Map

| File | Responsibility |
| --- | --- |
| Create: `ai-service/schemas/tool_calling.py` | Provider-neutral typed tool definitions, model tool calls, turn result, trace entry, and strict argument shape. |
| Modify: `ai-service/providers/base_llm_provider.py` | Add the typed tool-turn contract and safe unsupported-provider behavior without changing generate_json. |
| Modify: `ai-service/providers/openai_provider.py` | Translate the typed contract to constrained OpenAI function calls and parse response into the shared type. |
| Modify: `ai-service/config.py` | Add validated retrieval limits with the exact defaults in Global Constraints. |
| Create: `ai-service/tools/scheduling_evidence_tools.py` | Fixed allowlist schemas, trusted-context argument binding, tool dispatch, typed read wrappers, and result-size validation. |
| Create: `ai-service/agents/scheduling_evidence_retriever.py` | Classify refreshable gaps, run the bounded ReAct loop, validate/merge accepted results, and return a safe retrieval outcome. |
| Modify: `ai-service/agents/scheduling_validation_agent.py` | Accept an optional retriever and preserve the existing direct deterministic behavior when none is configured. |
| Modify: `ai-service/graph/workflow_graph.py` | Add the conditional retrieval node before existing dependency validation; maintain an explicit loop counter/deadline state. |
| Modify: `ai-service/main.py` | Construct the retrieval agent with current settings, configured provider, and existing backend read tools for the scheduling endpoint only. |
| Create: `ai-service/tests/test_tool_calling_contract.py` | Validate provider-neutral tool call and strict schema behavior. |
| Modify: `ai-service/tests/test_llm_providers.py` | Test OpenAI tool schema request, parsed call/terminal turn, malformed response and timeout classification. |
| Create: `ai-service/tests/test_scheduling_evidence_tools.py` | Test allowlist, argument binding, typed backend wrapper use, response bounds, and fail-closed behavior. |
| Create: `ai-service/tests/test_scheduling_evidence_retriever.py` | Test gap selection, bounded loop, provenance validation, merge rules, and safe stops. |
| Modify: `ai-service/tests/test_scheduling_validation_graph.py` | Test fast path, retrieval path, and blocked retrieval path without changing deterministic candidate expectations. |
| Modify: `ai-service/tests/test_scheduling_validation_api.py` | Test endpoint construction/wiring and confirm response contract and authorization remain unchanged. |

Do not modify InternalAgentToolsController or add endpoints unless implementation inspection proves that an approved read cannot be performed using its current GET routes. If so, stop implementation and submit an amended spec for review first.

---

### Task 1: Define refreshable evidence gaps and typed results

**Files:**
- Create: `ai-service/schemas/tool_calling.py`
- Create: `ai-service/tests/test_tool_calling_contract.py`
- Create: `ai-service/tests/test_scheduling_evidence_retriever.py`

**Interfaces:**
- Produce `ToolCall(id: str, name: str, arguments: dict[str, object])`, `ToolTurn(text: str | None, tool_calls: list[ToolCall])`, `ToolTraceEntry(tool_name: str, source_ids: list[str], source_version: str | None, result_class: str, elapsed_ms: int)`, and `EvidenceRetrievalResult(request: SchedulingValidationInput, safe_stop_reason: str | None, tool_trace: list[ToolTraceEntry])`.
- Produce a deterministic gap classifier that distinguishes supported persisted evidence gaps from missing/incomplete Member 1–3 analysis. Only supporting crop-profile metadata and a stock snapshot already referenced by valid Member 3 requirements are refreshable in the first release. Although the approved design lists additional read tools, this first dispatcher uses only results with a safe, one-to-one merge target in the existing scheduling input; it does not fetch weather, re-run Member 3, or synthesize analysis. A profile is stale if its verification time is in the future or its source/version/entity does not match the persisted request; no age threshold is invented. A mismatch in Member 1–3 output itself blocks for upstream reanalysis.
- A missing or unsuccessful coordinator/field/weather-resource output is never refreshable by this retrieval layer. Do not include weather forecast retrieval: reading a forecast does not recompute Member 3's risk analysis.
- Strict call arguments must reject unknown fields. The tool-turn schema must allow a terminal response with no tool calls. Do not invent an age-based freshness TTL: reject future verification timestamps and source/version or entity mismatches; GET availability is a fresh read snapshot, while any mismatch in persisted Member 1–3 analysis requires safe reanalysis/review.

- [ ] **Step 1: Write tests** named `test_tool_call_schema_rejects_unknown_fields`, `test_tool_turn_accepts_terminal_text_without_calls`, `test_missing_upstream_analysis_is_not_refreshable`, and `test_only_profile_or_referenced_stock_gaps_are_refreshable`, and `test_future_verified_at_or_source_version_mismatch_is_stale`.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest tests/test_tool_calling_contract.py tests/test_scheduling_evidence_retriever.py -q`; confirm the new tests fail because the schemas/classifier do not exist.
- [ ] **Step 3: Implement** the typed Pydantic contract and pure gap classifier. Include a reason code and required trusted entity IDs for each refreshable gap; never ask the LLM to classify it.
- [ ] **Step 4: Run the same tests** and confirm they pass; add assertions that input request objects remain unchanged during classification.

### Task 2: Add typed provider tool-call support

**Files:**
- Modify: `ai-service/providers/base_llm_provider.py`
- Modify: `ai-service/providers/openai_provider.py`
- Modify: `ai-service/tests/test_llm_providers.py`

**Interfaces:**
- Add `async def generate_tool_turn(self, prompt: str, tool_schemas: list[dict[str, object]]) -> ToolTurn` to BaseLLMProvider. Its default implementation raises ProviderConfigurationError with a stable safe message; existing JSON and crop-finding methods retain their behavior.
- OpenAIProvider implements the method with strict function schemas, tool choice limited to the supplied functions, SDK retries disabled for this operation, and the configured provider timeout.
- Parse only valid function calls into ToolTurn. A refusal, malformed call, unknown call shape, or provider error raises/classifies as LLMProviderError; do not execute calls in this provider layer.

- [ ] **Step 1: Write** `test_base_provider_tool_calls_fail_closed`, `test_openai_provider_returns_typed_tool_calls`, `test_openai_provider_returns_terminal_turn`, `test_openai_provider_rejects_malformed_tool_arguments`, and `test_openai_provider_tool_turn_timeout_is_classified`.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest tests/test_llm_providers.py -q`; confirm the new contract tests fail.
- [ ] **Step 3: Implement** the shared method and OpenAI adapter using the SDK version already pinned in requirements; keep all existing generate_json response paths intact.
- [ ] **Step 4: Run** `cd ai-service; python -m pytest tests/test_llm_providers.py -q`; confirm both existing provider behavior and new tool-call tests pass.

### Task 3: Implement fixed read-only tool dispatch

**Files:**
- Modify: `ai-service/config.py`
- Create: `ai-service/tools/scheduling_evidence_tools.py`
- Create: `ai-service/tests/test_scheduling_evidence_tools.py`

**Interfaces:**
- Add `scheduling_retrieval_enabled=False`, `scheduling_retrieval_max_rounds=3`, `scheduling_retrieval_max_tool_calls=5`, `scheduling_retrieval_timeout_seconds=45`, and `scheduling_retrieval_max_observation_bytes=16384`, each with environment aliases, bounds, and tests. No provider or tool is constructed for scheduling while disabled.
- Define `SchedulingEvidenceTools.execute(name: str, arguments: dict[str, object], *, workflow_id: UUID, agent_step_id: UUID, request: SchedulingValidationInput) -> object`.
- Expose only: `GetCropPlanContext`, `GetCropReferenceProfile`, and `GetResourceAvailability`. For resource availability, IDs must be derived from validated Member 3 requirement rows, never accepted from model arguments. All tool calls use existing BackendToolClient-backed typed wrappers and GET endpoints.
- Bind workflow/step IDs from trusted graph state; bind crop-plan, crop-type, profile, field, and resource IDs from validated request or prior typed tool results. The dispatcher rejects extra arguments and emits no arbitrary URL/path mechanism.
- Reject serialized observations larger than 16 KiB. Return a minimized typed observation with the source/provenance needed for deterministic validation.

- [ ] **Step 1: Write** `test_unknown_tool_makes_no_http_call`, `test_model_cannot_override_workflow_step_or_entity_ids`, `test_resource_ids_come_from_validated_requirements`, `test_existing_get_wrapper_is_used`, `test_oversized_observation_is_rejected`, and `test_missing_backend_token_stops_safely`.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest tests/test_scheduling_evidence_tools.py -q`; confirm failure before dispatcher implementation.
- [ ] **Step 3: Implement** only the fixed typed dispatch table and validated conversions. Do not create a write-capable generic dispatcher.
- [ ] **Step 4: Run** the same tests and confirm rejected calls never reach BackendToolClient.

### Task 4: Build bounded ReAct retrieval and safe merge

**Files:**
- Create: `ai-service/agents/scheduling_evidence_retriever.py`
- Modify: `ai-service/tests/test_scheduling_evidence_retriever.py`

**Interfaces:**
- Implement `SchedulingEvidenceRetriever.retrieve(request: SchedulingValidationInput, *, workflow_id: UUID, agent_step_id: UUID) -> EvidenceRetrievalResult`.
- If the gap classifier returns no refreshable gap, return the original request and no tool trace; do not call the provider.
- If a gap is refreshable, allow no more than 3 model turns, 5 total tool calls, and one 45-second monotonic deadline. Sequentially dispatch calls under the same deadline; stop immediately when the budget is exhausted.
- Validate every observation with its Pydantic response model and compare workflow/entity/profile/source/version/verified-at data against trusted request context. Reject mismatch; do not partially merge.
- Permit in-memory replacement only for the missing verified crop-reference evidence bundle or resource availability checks corresponding to existing verified requirements. Never rewrite upstream agent status, warnings, analysis, or provenance.
- Return a safe stop reason for no tool-call support, refusal, malformed/unknown call, timeout/cancellation, missing token, backend failure, invalid/stale/mismatched evidence, or exhausted budget. On any safe stop leave the original request intact.
- Log only sanitized workflow/step/revision IDs, tool name, result class, source IDs/version, elapsed time, and stop reason. Do not log prompts, credentials, full tool payloads, or personal/farm records.

- [ ] **Step 1: Write tests** named `test_complete_request_skips_provider_and_tools`, `test_profile_gap_merges_only_matching_verified_profile`, `test_stock_gap_refreshes_only_referenced_stock_checks`, `test_cross_workflow_or_source_mismatch_leaves_request_unchanged`, `test_profile_entity_mismatch_leaves_request_unchanged`, `test_candidate_revision_is_not_model_supplied_or_changed`, `test_unavailable_upstream_output_never_triggers_react`, `test_malformed_unknown_and_repeated_calls_stop_safely`, `test_tool_and_turn_budgets_are_hard_limits`, `test_overall_deadline_stops_loop`, `test_cancellation_leaves_original_request_unchanged`, `test_provider_or_tool_failure_returns_safe_stop`, and `test_terminal_turn_with_unfilled_gap_does_not_schedule`.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest tests/test_scheduling_evidence_retriever.py -q`; confirm expected initial failures.
- [ ] **Step 3: Implement** the async retrieval loop with injected provider/tools/settings and copy-on-success request updates. Use `asyncio.timeout` or the project's compatible timeout pattern with a monotonic overall deadline; do not retry outside the bounded loop.
- [ ] **Step 4: Run** the same test file; assert failed retrievals return the original request object/data and cannot yield a candidate.

### Task 5: Wire retrieval into scheduling graph and API

**Files:**
- Modify: `ai-service/agents/scheduling_validation_agent.py`
- Modify: `ai-service/graph/workflow_graph.py`
- Modify: `ai-service/main.py`
- Modify: `ai-service/tests/test_scheduling_validation_graph.py`
- Modify: `ai-service/tests/test_scheduling_validation_api.py`

**Interfaces:**
- Extend `SchedulingValidationAgent.__init__(retriever: SchedulingEvidenceRetriever | None = None)`. Preserve `check_dependencies`, `propose`, `assess_risk`, and direct deterministic behavior.
- The graph inserts a retrieval node before `validate_evidence`; it then runs the existing dependency validation, candidate, and risk nodes unchanged. Pass workflow/step/revision from trusted request/graph context, never model arguments.
- Construct the read-only tool wrappers and configured provider only for the scheduling endpoint when `scheduling_retrieval_enabled` is true. The feature setting defaults false; test that disabled retrieval constructs neither provider nor tool client. If disabled, or if no tool-call-capable provider or backend token is configured, keep existing dependency output and deterministic fast-path behavior.
- Preserve the HTTP route, auth dependency, response model, workflow ID, candidate revision, and approval flags.

- [ ] **Step 1: Write graph/API tests** `test_graph_fast_path_does_not_call_retriever`, `test_graph_retrieves_before_dependency_validation`, `test_graph_safe_stop_skips_candidate_generation`, and `test_scheduling_api_keeps_existing_auth_and_response_contract`.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest tests/test_scheduling_validation_graph.py tests/test_scheduling_validation_api.py -q`; confirm the new integration tests fail.
- [ ] **Step 3: Implement** conditional wiring while retaining existing scheduler methods and graph edges after validation.
- [ ] **Step 4: Run** the same tests and existing scheduling validation tests; confirm deterministic candidate outputs are unchanged for complete inputs.

### Task 6: Run full AI checks and review rollout boundary

**Files:**
- Modify only files listed above unless tests prove a specific necessary adapter or schema change.

- [ ] **Step 1: Run** `cd ai-service; python -m compileall -q .`; expected exit code 0.
- [ ] **Step 2: Run** `cd ai-service; python -m pytest`; expected all AI-service tests pass.
- [ ] **Step 3: Run** `git diff --check` and scan changed files for conflict markers, secrets, generated output, and accidental non-GET dispatch.
- [ ] **Step 4: Run** applicable CI-equivalent backend, React, and Flutter checks if shared files were touched; record unavailable local dependencies as unverified rather than passing.
- [ ] **Step 5: Confirm** unchanged complete-input deterministic output, no-write property, HumanApproval-only finalization, and approval-time inventory/schedule recheck tests.
- [ ] **Step 6: Commit** in focused commits on the Member 4 feature branch; do not merge or push directly to dev/main without the user's separate authorization.

## Completion Gate

The implementation is review-ready only if all acceptance criteria in the design spec pass, including the no-model fast path, strict tool allowlist, bounded retrieval, provenance/workflow validation, safe blocking on failures, unchanged deterministic candidate generation, and unchanged human approval/transaction safeguards.
