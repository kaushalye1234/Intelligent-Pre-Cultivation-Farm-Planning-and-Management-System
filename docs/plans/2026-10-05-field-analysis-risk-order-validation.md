# Field Analysis Risk Order Validation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Accept identical FieldAnalysis `identifiedRisks` values regardless of order while rejecting missing, additional, duplicate, and invalid risks.

**Architecture:** Keep the existing contracts and safety gates. Replace only ordered equality in the Python and ASP.NET validators with duplicate-aware set comparison; schema and enum validation continue to reject invalid values.

**Tech Stack:** Python, pytest, Pydantic, ASP.NET Core 8, EF Core 8, xUnit

---

### Task 1: Python regression tests

**Files:**
- Modify: `ai-service/tests/test_crop_field_analysis_agent.py`

1. Add a fixture helper for multiple persisted risk observations.
2. Test reordered-identical, missing, additional, duplicate, and invalid output risks.
3. Run `python -m pytest tests/test_crop_field_analysis_agent.py -q` and confirm the reordered case fails before implementation.

### Task 2: Python validator

**Files:**
- Modify: `ai-service/agents/crop_field_analysis_agent.py`

1. Require output risks to be distinct.
2. Compare output and submitted risks as sets.
3. Re-run the focused Python tests and confirm all pass.

### Task 3: ASP.NET regression tests

**Files:**
- Modify: `backend/AgriAssist.Api.Tests/CropPlanningAiWorkflowTests.cs`

1. Add table-driven workflow cases for reordered-identical, missing, additional, duplicate, and invalid risks.
2. Assert only reordered-identical output advances; all invalid collections remain retryable `SafeFailure` results.
3. Run the focused workflow tests and confirm the reordered case fails before implementation.

### Task 4: ASP.NET validator

**Files:**
- Modify: `backend/AgriAssist.Api/Services/CropPlanning/CropPlanningService.cs`

1. Retain existing invalid-enum and duplicate checks.
2. Replace `SequenceEqual` with `ToHashSet().SetEquals(...)` for membership comparison.
3. Re-run the affected .NET tests.

### Task 5: Verification and local commit

1. Run the affected Python and .NET suites.
2. Exercise a real FastAPI FieldAnalysis request with reordered risks; report an exact upstream failure if external dependencies block it.
3. Run conflict-marker checks and `git diff --check`.
4. Review the scoped diff and create a local commit only. Do not push or change branches.
