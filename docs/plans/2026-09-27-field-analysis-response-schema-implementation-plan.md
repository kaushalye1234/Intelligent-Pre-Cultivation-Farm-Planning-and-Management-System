# Field Analysis Response Schema Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enforce the existing field-analysis response contract at provider generation and Pydantic validation boundaries.

**Architecture:** Thread the existing Pydantic JSON schema through the provider abstraction, constrain OpenAI structured output, and keep evidence-derived semantic validation in `CropFieldAnalysisAgent`. Invalid output remains a retryable SafeFailure with sanitized structured diagnostics.

**Tech Stack:** Python 3.13, Pydantic 2, FastAPI, OpenAI provider, pytest, ASP.NET Core 8/xUnit.

---

### Task 1: Add failing contract tests

**Files:**
- Modify: `ai-service/tests/test_crop_field_analysis_agent.py`
- Modify: provider tests under `ai-service/tests/` if present

1. Add cases for `Completed`, `Normal`, object-valued assessments, missing `summary`, and `overallStatus`.
2. Add valid empty-array and `FloodingRisk` priority cases.
3. Assert the provider receives the aliased `CropFieldAnalysisOutput` JSON schema.
4. Run focused tests and confirm the new cases fail for the expected reasons.

### Task 2: Align the Pydantic contract

**Files:**
- Modify: `ai-service/schemas/common.py`
- Modify: `ai-service/schemas/field_analysis.py`

1. Reject unknown provider-output fields.
2. Restrict field-analysis status to `Analyzed | SafeFailure`.
3. Require `fieldCondition.summary` while preserving existing list defaults and null/list semantics.
4. Run focused schema/agent tests.

### Task 3: Enforce provider structured output and diagnostics

**Files:**
- Modify: `ai-service/providers/base_llm_provider.py`
- Modify: `ai-service/providers/openai_provider.py`
- Modify: `ai-service/agents/crop_field_analysis_agent.py`
- Modify: affected fake providers/tests

1. Add an optional response-schema parameter to `generate_json`.
2. Forward it using OpenAI's structured-output API.
3. Pass `CropFieldAnalysisOutput.model_json_schema(by_alias=True)` from the field-analysis agent.
4. Make the prompt explicit about types, enums, exact nested keys, and evidence-derived priority.
5. Log sanitized validation error metadata only; retain the concise user warning.
6. Run focused AI tests.

### Task 4: Verify integration behavior

1. Run the full AI suite.
2. Run focused backend field-analysis tests, then the full backend suite if practical.
3. Run relevant React workflow-review regression tests because the UI consumes SafeFailure output, without modifying React code.
4. Run applicable build/lint checks.
5. Run `git diff --check` and scan for conflict markers.

### Task 5: Commit the focused change

1. Confirm unrelated lockfiles are unstaged and unchanged by this work.
2. Stage only the AI-service and plan files used by this fix.
3. Commit with `fix: enforce field analysis response schema`.
4. Report the final branch, status, verification results, and commit hash.
