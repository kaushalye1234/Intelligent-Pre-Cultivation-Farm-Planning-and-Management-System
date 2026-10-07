# Member 4 evidence verification and recovery

Date: 2026-10-07
Status: Approved for implementation by the project owner on 2026-10-07; agronomic evidence remains unverified.
Base: `codex/member4-approval-guide` at `19d7cc8`; PR #87 remains separate and open.

## Purpose and success

An Agricultural Officer can resolve a Member 4 `MissingDependency` caused by an incomplete crop reference profile without losing the failed run's audit history. The page makes the missing evidence and next responsible role clear. It never treats a button click as agronomic evidence and never approves a candidate automatically. Success is a newly verified, source-cited profile; a replacement workflow whose Member 3 and Member 4 steps use that exact profile; an explicit officer approval; and a farmer plan showing approved work plus a dated guide or an honest Pending/Unavailable state.

The first target is Rice Bg 352 in Anuradhapura. Its water regime has not been verified. The old workflow `a0077749-2763-4ed9-b504-8072183abc9e` and its candidate remain blocked and immutable as history.

## Approach choice

A label or button on the existing reference table would be small, but it would leave water regime, source review, audit identity, and workflow restart unresolved. An automatic merger of the current rules-only and stage-only profiles would violate the pinned-source contract. Use a dedicated **Resolve required evidence** page with a server-enforced verification action and a guarded replacement workflow. The same Agricultural Officer may draft and verify; verification is still a separate explicit action with server-recorded identity and time.

## Officer experience

From a blocked workflow, the officer opens the resolution page. It shows the crop, variety, field, region, planned dates, Member 3 pinned profile ID, current stages and rules, exact blocking reasons, and links to persisted upstream outputs. It links the source-review notes as unverified guidance; it does not copy suggested rates into saved evidence.

The officer records the field's irrigated or rainfed regime and its supporting observation, then reviews the applicable source URLs, maturity stage, resource rates, units, inventory resource matches, and any irrigation rule. The page links each cited source beside entered values and rejects incomplete values; the officer judges whether the source supports them. A profile draft is inactive. **Verify and activate** displays the complete persisted draft and requires an affirmative decision; it records the actor and UTC time. A verified profile is immutable; corrections create a new draft version. A rules-only or stages-only draft cannot be activated for this recovery flow.

After verification, the page presents **Start replacement workflow** with a future planning window to an Admin; the Agricultural Officer sees the Admin handoff until that role acts. It shows progress through the existing Member 1, Field Officer, Resource Officer, and Member 4 steps with role-specific actions or links. It does not impersonate those roles or silently accept their decisions. The officer sees any new missing dependency and can return to the relevant evidence. The final candidate still requires a separate approve/reject/revision decision. After approval, the guide status and an officer-only retry action appear; farmer approved tasks remain visible even if guide generation fails.

## Data and API boundaries

Introduce `VerificationState` (`Draft`, `Verified`, `LegacyReviewRequired`), nullable server-managed `VerifiedByUserId` and `VerifiedAt`, and an integer `DraftVersion` concurrency token. Store field water-regime evidence in a field-scoped verification record linked to the profile verification decision. New drafts have no verification actor or time. Existing profiles retain their data and become `LegacyReviewRequired`; migration never asserts that an officer reviewed them. Do not accept client-supplied verification time as proof. Admin may prepare a draft; Agricultural Officer alone verifies it. The same officer may be the creator. Verification validates crop/variety/region scope, stage and rule presence, structured rule schema, source citations, field regime, and applicable resource IDs and units. It cannot assert that a source supports a value without the officer's explicit review. Keep draft edits separate from verified versions. The recovery path requires `Verified` and an active profile. Existing active legacy profiles remain available to unrelated legacy flows during this rollout, but are labeled for review; completed decisions remain intact.

Add a workflow-scoped required profile ID for the replacement run. Member 3 requirement selection and Member 4 scheduling must both use it or return `MissingDependency`. Scope this pin to the replacement path, so unrelated legacy workflows are not silently repointed. Server checks the field's verified water regime against the profile's regime before starting the replacement run. The reference profile and verification record are read consistently when starting each upstream step; changed or deactivated evidence blocks continuation.

Add an Admin-only idempotent replacement-start endpoint for a request whose latest workflow is `MissingDependency` or `CandidateBlocked`. It creates a new `AgentWorkflow` against the same `CropPlanRequest`, with an explicit `SupersedesWorkflowId`, fresh step history, and the verified profile pin. It checks that the request is not approved, rejected, cancelled, or otherwise active; validates a future planning window and field access; and rejects simultaneous starts. The old workflow, outputs, candidate, and decisions are never edited. This endpoint is the recovery path because `start-ai-workflow` currently rejects a `PreliminaryGenerated` request, while a second active request for the same field and crop is rejected. The replacement progresses through the existing role-restricted Member 1–3 endpoints and Member 4 candidate endpoint. Where a human prerequisite is outstanding, the UI shows the responsible role and waits.

No final task, irrigation schedule, or reservation is created before approval. The approval transaction retains its current stale-revision, stock, schedule, idempotency, and concurrency checks. Guide generation remains downstream of successful approval; failure records `Unavailable` and permits the existing protected retry for the same approved revision. Flutter displays status, generation date, and refresh.

## Failure handling and rollout

Draft validation returns field-specific errors. Verification is atomic and rejects concurrent edits or verification attempts. Replacement start returns a stable existing replacement on an identical idempotency key and conflicts on an altered request. Wrong role, wrong field, mismatched profile ID, stale profile version, missing source, unsupported water regime, failed upstream step, and unavailable AI service stop at a named stage without final records. A changed guide revision is never shown to the farmer.

Implement and prove this first with disposable PostgreSQL and synthetic users, including same-officer verification, denied roles, legacy profile handling, incomplete drafts, concurrent verification/start, source mismatch, replacement links, upstream handoff, approval, rollback, guide failure/retry, and farmer ownership. Run backend, AI, React, and Flutter checks. Ship through reviewed PRs; inspect deployed migration history and backup before applying any migration. A real live approval and farmer guide demonstration occurs only after an Agricultural Officer verifies the field regime and profile values and makes the final approval decision. Record deployed commit IDs and API/Flutter evidence.

## Out of scope

The page does not infer irrigated/rainfed status from location, merge existing profiles, auto-approve a candidate, rewrite the old failed workflow, or continuously refresh generated agronomic advice. A separate audit can decide how to re-review all legacy profiles beyond this recovery path.
