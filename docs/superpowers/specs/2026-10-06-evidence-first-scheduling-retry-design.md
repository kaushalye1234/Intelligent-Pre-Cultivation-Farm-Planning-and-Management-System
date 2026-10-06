# Evidence-First Scheduling Retry

## Intent

Give Agricultural Officers a usable recovery path when Member 4 scheduling is blocked by incomplete or stale evidence. Help the officer locate missing resource requirements with the existing AI research workflow, let an Admin verify cited facts, and rerun downstream analysis after evidence or the crop window changes.

## Existing behavior

- `MissingDependency` can call the existing candidate generation endpoint, but `CandidateBlocked` has no retry action in the review page.
- Candidate generation reads the stored Member 3 weather/resource output. That output is not refreshed after a verified resource rule changes.
- Resource requirement AI research already returns a sourced draft and has a separate verify-and-save action. Both actions are currently Admin-only.
- Crop plan requests with workflow history cannot update their date window, so a too-short window has no correction path in the current workflow.

## Design

1. Add a workflow retry operation for Agricultural Officers and Admins. It accepts an expected workflow version, an optional revised preferred start/end date, and a required reason when the dates change. It is available only for unapproved, nonterminal workflows in `MissingDependency`, `CandidateBlocked`, `Failed`, or `RevisionRequested`.
2. Date changes are saved on the original request with `CropPlanRequestHistory` audit data. Crop, variety, farm, and field remain unchanged. The officer must choose the dates; AI may show a suggested end date derived from a verified crop stage but may not apply it automatically.
3. Before scheduling retry, rerun the Weather/Resource analysis from the verified field-analysis handoff so changed requirements and weather are reflected. Then run Member 4 scheduling against the refreshed output. Record the new candidate revision and validators. A running, approved, rejected, or cancelled workflow cannot retry.
4. Expose the existing resource-research panel to Agricultural Officers from the workflow review. The AI response remains a draft with citations, warnings, and `Verified=false`. Agricultural Officers can inspect the draft; only an Admin may verify and save it. The Admin action persists the source, evidence, identity, and timestamp.
5. Keep candidate creation and final approval gates unchanged. If evidence is unavailable, conflicting, unsupported, or the proposed window still fails validation, show the reason and allow another correction/retry where the workflow is eligible. No tasks, irrigation schedules, or reservations are created until an approvable candidate is explicitly approved.

## Human flow

`Blocked reason → AI source-backed draft (optional) → Admin checks and verifies evidence → officer corrects dates if needed → Retry scheduling refreshes Member 3 and Member 4 → officer reviews a ready candidate → explicit approval`

## Out of scope

- Treating model-generated values as verified facts.
- Allowing approval of a blocked candidate or bypassing deterministic validation.
- Automatically changing the crop, variety, farm, field, inventory, or requested dates.
- Rerunning or rewriting the persisted Member 1 and Member 2 analyses; the date-window adjustment does not alter their stored field observations.

## Acceptance criteria

- Agricultural Officers and Admins can retry eligible blocked workflows; other roles cannot.
- A retry after a resource rule is verified refreshes Member 3 before Member 4 uses the new result.
- An officer can correct the date window on an eligible workflow, and the old/new window and reason are auditable.
- A retry is rejected for stale versions, concurrent runs, and terminal/approved workflows.
- AI research creates no verified rule until an Admin explicitly confirms a cited recommendation; Agricultural Officers cannot save AI findings as verified data.
- Missing or conflicting evidence and too-short windows still prevent approval; corrected evidence can yield a normal `PendingOfficerApproval` candidate.
- All existing approval, transaction, inventory, and concurrency safeguards remain in force.
