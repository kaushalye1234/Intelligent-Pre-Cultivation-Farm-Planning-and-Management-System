# Member 2 AI-Assisted Inspection Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add optional, human-controlled inspection note assistance and representative crop-image analysis while preserving the authoritative post-submission Field Analysis, Member 3/4 handoffs, and farmer approval boundary.

**Architecture:** ASP.NET remains the authorization, persistence, concurrency, and approval authority. Two internal Python capabilities use the existing OpenAI provider: a stateless one-call note assistant and a deterministic two-call image-analysis pipeline with non-LLM trusted-source retrieval. Only a frozen Field Officer review can enter Field Analysis; Member 4 maps allowlisted non-chemical actions and explicitly approves farmer guidance; Flutter consumes a dedicated farmer-safe projection.

**Tech Stack:** ASP.NET Core 8, EF Core 8, PostgreSQL, Cloudinary, FastAPI, Pydantic, OpenAI Responses API, React/TypeScript/Vite, Flutter/Dart, xUnit, pytest, Vitest, Flutter test.

---

## Scope and non-negotiable invariants

- Remain on `Field-Inspection-&-Crop-Issue-Management`; create exactly ten local commits and never push.
- Preserve `CropFieldAnalysisAgent` as the authoritative post-submission agent. Pre-submit assistance never mutates its `AgentStep`.
- Note suggestions are transient. Only the existing inspection save operation persists officer-controlled note text.
- Keep multiple inspection images. At most one `InspectionImage` is representative for AI through a PostgreSQL partial unique index.
- Image analysis is optional and explicitly started. Submission never waits for it.
- One analysis performs at most two model calls: Vision Pass 1 and Grounded Pass 2. No model search, retry loop, or autonomous tool loop.
- Raw image analysis is immutable after one `Running -> terminal` transition. Reviews are append-only.
- Submission atomically freezes an eligible Accepted/Edited review ID or null.
- Only allowlisted non-chemical crop-health action identifiers cross into Member 4. Backend-owned wording is authoritative.
- Farmers receive only a server-composed approved-plan DTO; no raw workflow, AI, audit, fingerprint, or staff-note data.
- Preserve role values Farmer `1`, FieldOfficer `2`, ResourceOfficer `3`, AgriculturalOfficer `4`, Admin `5`.
- Use UTC persistence, restrictive audit foreign keys, current soft-deletion conventions, and PostgreSQL verification for locks/partial indexes.

## Approved lifecycle

```text
Draft structured observations
  -> transient InspectionNoteAssistantAgent suggestions
  -> officer Accept/Edit/Reject in local React state
  -> existing Save Draft persists only current form text

Inspection evidence uploads
  -> officer selects one representative image
  -> explicit Analyze Image
  -> verified original bytes
  -> transient sanitized derivative
  -> Vision Pass 1
  -> deterministic Sri Lanka-first evidence discovery
  -> Grounded Pass 2
  -> immutable terminal analysis
  -> append-only officer Accept/Edit/Reject review
  -> submission freezes exact eligible review ID
  -> successful CropFieldAnalysisAgent output
  -> deterministic reviewedCropIssueActions
  -> optional Member 3 considerations
  -> deterministic Member 4 candidate mapping
  -> AO/Admin task decisions and explicit guidance Include/Reject
  -> approved work and farmer-safe approved-plan DTO
  -> Flutter
```

## Contracts and ownership

Use explicit contract versions and strict unknown-field rejection.

| Contract | Initial version | Authority |
|---|---:|---|
| Inspection note assistance | 1 | Python schema + ASP.NET validation |
| Image-analysis Pass 1 | 1 | Python schema |
| Final inspection image analysis | 1 | Python schema + ASP.NET validation |
| Reviewed image-analysis projection | 1 | ASP.NET domain |
| Crop Field Analysis | 2 | Existing Python/C# contract, additive actions |
| Member 3 crop-health consideration | 1 | Member 3 contract, action-linked only |
| Member 4 crop-health proposal/guidance | 1 | ASP.NET approval domain |
| Farmer approved plan | 1 | ASP.NET farmer-safe DTO |

The ASP.NET crop-health action catalog owns identifiers, categories, timing constraints, responsible-role constraints, task wording, and farmer wording. Python mirrors identifiers only. Shared fixtures live under `docs/ai-usage/contracts/member-2-crop-health/` and are loaded by C# and Python tests.

Initial allowlisted actions:

- `FieldSanitation`
- `RemoveAffectedResidue`
- `SeparateAffectedMaterial`
- `InspectNearbyPlants`
- `MonitorSymptoms`
- `PrePlantingCleanup`
- `RequestFurtherAssessment`

No action type or wording can represent pesticide, fungicide, herbicide, active ingredient, dose, concentration, interval, or spray schedule.

## Persistence model

Modify `InspectionImage` with:

- `IsRepresentativeForAi` default false.
- `ContentSha256`.
- immutable Cloudinary asset/version/delivery metadata required for controlled retrieval.
- PostgreSQL partial unique index on `FieldInspectionId` where `IsRepresentativeForAi` is true.

Modify `FieldInspection` with nullable restrictive `FrozenImageAnalysisReviewId`. It is backend-derived in the submission transaction and immutable after completion.

Create `InspectionImageAnalysis` with:

- inspection/image foreign keys and immutable ID;
- composite fingerprint and materially relevant input snapshot;
- provider/model, prompt/schema, preprocessing, source-policy, and relevance-rule versions;
- one-way status: `Running` to exactly one of `Succeeded`, `Failed`, `TimedOut`, `Interrupted`, or `Stale`;
- terminal Pass 1 JSON, exact bounded evidence packet JSON, final result JSON, sanitized failure, and completion timestamp;
- concurrency protection and partial uniqueness preventing duplicate Running/Succeeded work for one fingerprint.

Create append-only `InspectionImageAnalysisReview` with analysis/reviewer, `Accepted|Edited|Rejected`, typed reviewed projection, edited-field/action markers, staff-only note, action order, and UTC timestamp. Terminal analyses and review rows are never updated.

Create one focused migration and update `AppDbContextModelSnapshot` in the same commit.

## Fingerprint

Canonical SHA-256 input includes only result-affecting values:

- inspection image database ID;
- verified original content hash;
- Cloudinary/storage revision;
- canonical crop ID and optional variety ID;
- any additional context actually sent to the image agent;
- source-policy version/content hash;
- image-analysis schema and prompt/contract versions;
- image preprocessing version;
- relevance/coverage/deduplication rule version;
- provider and model identifiers.

No unrelated workflow field enters the fingerprint. A mismatch makes the old result non-current without mutating it. No automatic rerun occurs.

## Concurrency protocol

All state-changing operations use a short PostgreSQL transaction and lock the target `FieldInspection` row using the repository's existing `FOR UPDATE` pattern:

- representative selection;
- analysis startup/deduplication;
- terminalization/recovery;
- review append;
- submission and frozen-review capture.

Never hold the lock across Cloudinary, HTTP retrieval, or OpenAI calls. Analysis startup inserts `Running` then commits. Terminalization reacquires the row, revalidates current status/image/fingerprint/submission, and performs one conditional update. Abandoned Running rows may become Interrupted on the next relevant access; they are never auto-retried.

## Cloudinary and image integrity

- New uploads compute SHA-256 before upload and persist Cloudinary asset/version/delivery metadata.
- Use authenticated/private delivery for new evidence where supported by the installed SDK.
- Authorized staff viewing goes through ASP.NET and returns a short-lived signed capability or controlled response.
- Legacy public assets remain supported without bulk migration; never claim old disclosed URLs became private.
- AI retrieval uses a new internal tool authenticated by the existing backend tool token. It resolves a Running analysis snapshot, fetches the exact asset, validates MIME/size/revision, recomputes SHA-256, and returns bounded bytes/base64.
- Never trust client or model URLs. Never log signed URLs, bytes, base64, secrets, or EXIF.

The Python agent preprocesses verified bytes transiently: safe decode, decompression/pixel guards, EXIF orientation, longest-edge cap, aspect preservation, RGB/approved format conversion, metadata removal, and bounded encoded size. Only this derivative reaches OpenAI. `IMAGE_PREPROCESSING_VERSION` must match the request; the derivative is never persisted.

## Generic provider extension

Add one domain-neutral strict structured-generation method to `BaseLLMProvider` and `OpenAIProvider` while preserving every current method/caller. The request contains schema name/version, strict JSON Schema, bounded system/user input, optional controlled image bytes/MIME, and operation timeout. The OpenAI implementation uses Responses API, `store=false`, no retries, and exposes only safe provider/model metadata.

Detect and type refusal, incomplete output, timeout, malformed structured data, schema mismatch, and provider failure. Agents revalidate with Pydantic. Automated tests use fakes only.

## Inspection Note Assistant

Add a dedicated stateless FastAPI service/endpoint. No database, tools, web, image, agent loop, or workflow state.

Input: typed current unsaved structured observations plus minimal backend-derived crop/variety/field context. One explicit request performs one text-only provider call.

Output: strict fixed fields `SoilNotes`, `WaterConcerns`, `DrainageNotes`, `GeneralFieldNotes`, `RiskNotes`, and `OfficerNotes`, plus `contradictionWarnings` and `missingDataWarnings`. Unknown fields are rejected. Insufficient evidence yields empty suggestion/warning, never invented facts.

React displays each value as `AI Suggested Draft` with independent Accept/Edit/Reject. Accept/Edit change local form state only; Reject dismisses only that suggestion. Generation/regeneration is explicit. Failures leave manual save/submission usable.

## Inspection Image Analysis Agent

Create a deterministic acyclic pipeline:

```text
RetrieveImage -> PreprocessImage -> VisionPass1 -> TrustedSourceRetrieval
-> GroundedPass2 -> ValidateResult
```

Pass 1 is one 30-second structured multimodal call producing concise visible findings, possible issue categories, severity indicators, uncertainty, further-assessment flag, and no more than three category-level search intents with one primary intent. It never confirms a diagnosis or creates chemical guidance.

Trusted retrieval is non-LLM and reuses Member 1 policy/security primitives without changing Crop Finding behavior. Do not invoke `CropFindingAgent` or `OpenAIProvider.search_web`.

Hard operation limits:

- overall AI-service operation: 120 seconds, no retry;
- retrieval: 35 seconds;
- Stage 1 entries 4, documents 6, depth 1, candidate links inspected 20/page;
- Stage 2 entries 3/documents 4 only when Stage 1 has zero usable evidence;
- final evidence documents 3;
- 8,000 characters/source and 20,000 total ceilings, normally much less;
- reuse connect 4s, read 12s, document 18s, redirects 3, HTML 2 MB, PDF 10 MB.

Pre-rank entries and candidate links deterministically. A usable document must pass source controls, minimum readable content, canonical crop match, approved issue/search-intent match, and weighted URL/title/heading/link/body threshold. A stricter strong-evidence rule permits early stop when the primary concern and a non-chemical/assessment action are covered. Use deterministic normalized token-shingle deduplication and stop when remaining candidates add no material coverage. Version relevance, synonyms, coverage, and deduplication rules.

Persist the exact post-validation, post-ranking, post-deduplication evidence packet sent to Pass 2: policy ID/stage, organization/category, final URL/title, retrieval time, normalized document hash, relevance signals/version, and exact extract. Never persist entire external documents.

Pass 2 is one 45-second text-only strict call using only compact Pass 1 findings, crop/variety context, and selected evidence. It returns visible findings, uncertain possible categories/issues, severity, uncertainty, source references, allowlisted action identifiers, and further-assessment flag. With no grounding, preserve observations, conservative non-chemical actions only, and force further assessment. Pass 2 failure produces no final result.

## Review and submission freeze

While `InProgress`, the owning Field Officer may select/change the representative image, analyze, and append Accept/Edit/Reject reviews. Accept copies the final typed result. Edit changes bounded structured findings/concerns/severity/uncertainty/further-assessment and selects/reorders only allowlisted actions. A staff-only note is never a downstream action. Reject has no eligible projection.

Every review is immutable. Original sources are read-only. Accepted AI actions retain source links; officer-added actions are marked and inherit no source claims.

Submission remains allowed with no image, no analysis, failed/pending analysis, or unreviewed/rejected result. React shows a non-blocking warning for pending/unreviewed current analysis. Under the inspection lock, submission derives current analysis/review and stores the exact Accepted/Edited review ID or null before marking completed. Late completions become Stale.

## Field Analysis and downstream contracts

Add optional `reviewedImageAnalysis` input derived only from `FrozenImageAnalysisReviewId`. Never accept a client-selected trusted analysis/review.

After a successful model-backed or existing deterministic-fallback Field Analysis result, the wrapper deterministically constructs ordered `reviewedCropIssueActions` from the frozen review and backend catalog. The model never authors this collection. Any SafeFailure/invalid result blocks workflow advancement and the actions do not bypass the gate.

Member 3 receives actions as read-only optional context and may emit separate action-linked weather/resource considerations. It cannot echo-as-authority, rewrite, remove, approve, reclassify, or strengthen actions.

Member 4 receives original actions plus optional Member 3 considerations. A deterministic catalog maps each action to pre-planting task, early-growth monitoring, assessment escalation, or farmer guidance only. Semantic fields and wording are locked; AO/Admin can include/reject and change only permitted dates/windows, role/assignee, priority, or staff-only scheduling note. Backend validation mirrors the catalog.

Construct a locked typed crop-health guidance candidate from the frozen review, successful Field Analysis, and catalog. If present, proposal decision starts `PendingDecision`; if absent, `NotApplicable`. Final approval requires explicit `Included` or `Rejected`. A new proposal version resets the decision. Guidance and task decisions are independent.

## Farmer-safe approved plan

Add a dedicated authenticated endpoint following existing routing conventions, conceptually `GET /api/crop-plans/{id}/approved-plan`. It is available only for an approved plan and authorized owning farmer. Compose at read time from approved authoritative plan/work/task/irrigation/proposal data; do not create a projection table.

The optional crop-health section contains only included approved guidance and approved catalog actions: observation, possible concern, uncertainty/escalation, pre-planting actions, monitoring guidance, and explanation. Preserve uncertainty. Exclude all internal IDs, hashes, versions, raw JSON, source-policy details, audit history, staff notes, rejected/stale content, and unapproved candidates.

Legacy approved plans continue to return existing content with no inferred/backfilled crop-health section. Flutter consumes this DTO and conditionally extends the current approved-plan screen; it no longer parses raw workflow review for farmer presentation.

## Role-scoped views

- Owning Field Officer: current analysis, raw result, comparison, review history, sources, and explicit audit history; mutation only while InProgress.
- Authorized AO/Admin: submitted representative image, frozen raw/reviewed result, uncertainty, edit markers, source provenance, and Field Analysis context read-only.
- Resource Officer: typed actions and relevant Member 3 context only.
- Farmer: approved-plan DTO only.
- Detailed stale/rejected/failed/interrupted/superseded records appear only in explicit authorized staff audit DTOs, never normal downstream views.

## API shape

Extend the existing crop-plan/pre-planting assessment controller/service boundary with nested operations for:

- transient note suggestions;
- representative image selection;
- explicit analysis start;
- current analysis status/result;
- append current-analysis review;
- authorized evidence viewing;
- staff audit history.

Only representative selection accepts an image ID. Analyze accepts no URL, hash, fingerprint, or trusted analysis ID. Review resolves the current reviewable analysis server-side; any client token is stale-UI protection only. React talks only to ASP.NET. ASP.NET uses the service token to call Python; Python uses the existing tool token to call the narrow image-byte tool.

## Failure semantics

Both new AI capabilities are optional. Missing provider/model/key or provider outage does not break startup or manual work. Notes return typed unavailable/safe failure. Image analysis transitions Running to Failed before retrieval/Pass 2 when provider availability fails. No synthetic AI fallback and no automatic retry.

Sanitize stored/logged failures. Never log keys, tokens, provider payloads, image/base64, signed URLs, evidence text, or EXIF. Existing other-agent fallbacks remain unchanged.

## Exactly ten commits

1. `docs(member2): approve AI-assisted inspection plan`
2. `feat(member2): add image analysis persistence model`
3. `feat(member2): secure inspection evidence delivery`
4. `feat(member2): add transient inspection note assistant`
5. `feat(member2): add structured vision analysis pipeline`
6. `feat(member2): add deterministic trusted evidence grounding`
7. `feat(member2): add image review and field analysis handoff`
8. `feat(workflow): carry approved crop health guidance`
9. `feat(ui): add staff AI review and farmer plan views`
10. `test(member2): complete contract and integration coverage`

Tests belong with each implementation commit where practical; commit 10 adds shared golden fixtures, cross-layer/concurrency/regression coverage, final contract documentation, and cleanup rather than postponing all tests.

---

## Task 1: Commit the approved plan

**Files:**

- Create: `docs/plans/2026-10-03-member2-ai-assisted-inspection.md`

**Steps:**

1. Verify branch, clean tree, and absence of conflict markers.
2. Review the plan against all approved decisions.
3. Run `git diff --check`.
4. Commit only this file as commit 1.

## Task 2: Add persistence foundations

**Files:**

- Modify: `backend/AgriAssist.Api/Models/Inspections/FieldInspection.cs`
- Modify: `backend/AgriAssist.Api/Models/Inspections/InspectionImage.cs`
- Create: `backend/AgriAssist.Api/Models/Inspections/InspectionImageAnalysis.cs`
- Create: `backend/AgriAssist.Api/Models/Inspections/InspectionImageAnalysisReview.cs`
- Modify: `backend/AgriAssist.Api/Data/AppDbContext.cs`
- Create: one EF migration and designer
- Modify: `backend/AgriAssist.Api/Migrations/AppDbContextModelSnapshot.cs`
- Test: backend model/migration tests

**Steps:**

1. Write model tests for defaults, enums, relationships, terminal immutability helpers, and append-only review shape.
2. Add entities/enums with bounded fields and restrictive relationships.
3. Configure JSONB payloads, indexes, partial unique constraints, UTC/audit fields, and concurrency token.
4. Generate one migration and inspect SQL/snapshot.
5. Run focused backend tests and migration checks.
6. Commit as commit 2.

## Task 3: Secure evidence storage and retrieval

**Files:**

- Modify: `backend/AgriAssist.Api/ExternalServices/Cloudinary/*`
- Modify: `backend/AgriAssist.Api/Services/Inspections/InspectionService.cs`
- Modify: `backend/AgriAssist.Api/Controllers/Inspections/InspectionsController.cs`
- Modify: `backend/AgriAssist.Api/Controllers/CropPlanning/CropPlansWorkflowController.cs`
- Modify: `backend/AgriAssist.Api/Controllers/Internal/InternalAgentToolsController.cs`
- Modify: `backend/AgriAssist.Api/Dtos/CropPlanning/InternalAgentToolDtos.cs`
- Modify: `backend/AgriAssist.Api/Program.cs`
- Test: upload/hash, signed view, ownership, representative selection, internal retrieval, PostgreSQL race tests

**Steps:**

1. Add failing tests for hash/version persistence and representative uniqueness.
2. Hash validated upload bytes and use authenticated Cloudinary delivery for new assets.
3. Add authorized short-lived viewing and legacy compatibility.
4. Add locked representative selection service/route.
5. Add analysis-scoped internal retrieval with tool-token authentication, exact asset validation, and byte hashing.
6. Verify no bytes/URLs/secrets are logged.
7. Run focused backend and PostgreSQL tests.
8. Commit as commit 3.

## Task 4: Add transient note assistance

**Files:**

- Modify: `ai-service/providers/base_llm_provider.py`
- Modify: `ai-service/providers/openai_provider.py`
- Modify: `ai-service/providers/__init__.py`
- Create: `ai-service/schemas/inspection_note_assistance.py`
- Create: `ai-service/agents/inspection_note_assistant_agent.py`
- Modify: `ai-service/main.py`
- Modify: backend AI client/DTO/service/controller files
- Test: provider and note agent/API/backend authorization tests

**Steps:**

1. Add provider fake tests for valid text/image structured output, refusal, incomplete, malformed, timeout, and provider failure.
2. Add the additive Responses API structured method with `store=false` and no retry.
3. Add strict note schemas and one-call stateless agent.
4. Add the protected internal Python route.
5. Add nested ASP.NET note endpoint with typed unsaved input and backend-derived context.
6. Test missing provider is non-blocking and manual operations still work.
7. Commit as commit 4.

## Task 5: Build the vision pipeline

**Files:**

- Modify: `ai-service/requirements.txt`
- Create: `ai-service/schemas/inspection_image_analysis.py`
- Create: `ai-service/tools/inspection_image_analysis_tools.py`
- Create: `ai-service/agents/inspection_image_analysis_agent.py`
- Modify: `ai-service/config.py`
- Modify: `ai-service/main.py`
- Modify: backend AI client and image-analysis service files
- Test: preprocessing, two-call ceiling, failure short-circuit, version mismatch, timeout tests

**Steps:**

1. Add strict schemas and safe-failure types.
2. Add one focused image library only if none exists.
3. Implement safe transient preprocessing and version validation.
4. Implement fixed non-looping pipeline with mocked retrieval and two provider calls.
5. Add 120/30/35/45-second bounded settings with validation.
6. Add ASP.NET startup/deduplication/terminalization orchestration without holding database locks externally.
7. Test provider unavailability, late completion, and process-interrupted recovery.
8. Commit as commit 5.

## Task 6: Add deterministic trusted evidence grounding

**Files:**

- Refactor minimally: `ai-service/tools/crop_finding_tools.py`
- Create/modify shared source-policy primitive module only if required
- Modify: `ai-service/tools/inspection_image_analysis_tools.py`
- Modify: `ai-service/agents/inspection_image_analysis_agent.py`
- Modify: `ai-service/config.py`
- Test: Member 1 regression plus Member 2 traversal/relevance/dedup/budget tests

**Steps:**

1. Preserve current Member 1 imports and tests.
2. Implement policy-entry pre-ranking, shallow traversal, validation, and bounded extraction without a model call.
3. Implement crop+issue mandatory gates and explicit weighted scoring.
4. Implement strong-evidence coverage, token-shingle novelty, early stop, and Stage 2 zero-usable fallback.
5. Build the exact compact evidence packet and content hashes.
6. Feed only the bounded packet to Pass 2 and persist the returned provenance through ASP.NET terminalization.
7. Assert maximum two provider calls and all budgets.
8. Commit as commit 6.

## Task 7: Add review, freeze, and Field Analysis handoff

**Files:**

- Modify: backend inspection/crop-planning services, validators, DTOs, and nested controller
- Modify: `ai-service/schemas/field_analysis.py`
- Modify: `ai-service/agents/crop_field_analysis_agent.py`
- Modify: `ai-service/tools/inspection_tools.py`
- Test: append-only review, stale review, submission races, frozen pointer, model/fallback action carry-forward

**Steps:**

1. Add the authoritative C# action catalog and strict reviewed projection validators.
2. Implement current-analysis status, audit view, and locked append-only review operations.
3. Implement submission warning metadata and atomic frozen-review capture.
4. Add optional reviewed image input to Field Analysis.
5. Deterministically attach ordered actions after successful model or fallback analysis.
6. Reject mismatched provenance/unknown actions; never forward after SafeFailure.
7. Run focused unit and PostgreSQL race tests.
8. Commit as commit 7.

## Task 8: Integrate Member 3, Member 4, approval, and farmer API

**Files:**

- Modify: Member 3 C#/Python schemas/services/agent tests
- Modify: `ai-service/schemas/scheduling_validation.py`
- Modify: `ai-service/agents/evidence_scheduler.py`
- Modify: backend Task Approval DTOs, validators, evidence builder, approval service/controller
- Add/modify farmer approved-plan DTO/service/controller route
- Update: `docs/ai-usage/member-2-crop-planning-contract.md`
- Update: `docs/ai-usage/member-3-weather-resource-contract.md`
- Test: considerations, deterministic mapping, revisions, guidance tri-state, legacy projection

**Steps:**

1. Add optional action-linked Member 3 considerations without action mutation.
2. Add deterministic action-to-task/guidance catalog mapping and provenance.
3. Lock semantic fields; validate operational-only revision fields.
4. Add locked guidance candidate and Pending/Included/Rejected/NotApplicable decision.
5. Reset decision on new proposal version and block approval while pending.
6. Add farmer-safe approved-plan endpoint with ownership/approved-state checks and legacy-empty crop health.
7. Verify approval transactions still recheck scheduling/inventory and create final work only after approval.
8. Commit as commit 8.

## Task 9: Implement React and Flutter experiences

**Files:**

- Modify: `frontend/react-app/src/api/client.ts`
- Modify: `frontend/react-app/src/types.ts`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.css`
- Modify: `frontend/react-app/src/pages/WorkflowReviewPage.tsx`
- Modify: related React tests
- Modify: `mobile/flutter_app/lib/models/api_models.dart`
- Modify: `mobile/flutter_app/lib/services/api_client.dart`
- Modify: `mobile/flutter_app/lib/screens/approved_crop_plan_screen.dart`
- Modify: related Flutter tests

**Steps:**

1. Add typed API methods without exposing internal tokens/URLs.
2. Add field-keyed AI Suggested Draft cards and per-field local actions.
3. Add representative selection, explicit Analyze, pending/unreviewed submit warning, structured review, and staff audit states.
4. Add AO/Admin locked guidance Include/Reject control and approval gate.
5. Keep the existing organic/utilitarian AgriAssist visual system; extend existing cards, notices, controls, responsive grids, and accessibility behavior rather than redesigning the app.
6. Migrate Flutter to the farmer-approved-plan DTO and conditionally render crop-health guidance.
7. Run React lint/build/tests and Flutter analyze/tests when installed.
8. Commit as commit 9.

## Task 10: Complete fixtures, regressions, documentation, and verification

**Files:**

- Create: `docs/ai-usage/contracts/member-2-crop-health/*.json`
- Modify: Python/C#/React/Flutter contract and integration tests
- Update: architecture/testing documentation and stale Gemini toolchain note
- Do not change runtime Gemini code because none exists

**Steps:**

1. Add shared valid/invalid golden fixtures and cross-language version/enum tests.
2. Add all remaining authorization, no-secret/no-raw-data, legacy-plan, rejected-guidance, no-image, concurrency, budget, and regression cases.
3. Confirm tests never make paid OpenAI calls or depend on live trusted websites.
4. Run backend restore/build/test, AI compile/pytest, React lint/build/test, and Flutter checks when available.
5. Run `git diff --check`, conflict-marker scan, secret/build-artifact scan, and full status review.
6. Commit as commit 10.
7. Verify exactly ten commits were added relative to the starting HEAD and no push occurred.

## Verification commands

```powershell
dotnet restore backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj
dotnet build backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-restore
$env:Logging__EventLog__LogLevel__Default = 'None'
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-build
```

```powershell
Set-Location ai-service
python -m pip install -r requirements.txt
python -m compileall -q .
python -m pytest
```

```powershell
Set-Location frontend/react-app
npm ci
npm run lint
npm run build
npm test
```

```powershell
Set-Location mobile/flutter_app
flutter pub get
flutter analyze
flutter test
```

PostgreSQL-specific partial-index, row-lock, transaction, JSONB, and concurrent-approval behavior must be reported verified only when the isolated PostgreSQL suite runs successfully.

## Implemented contract/version index

| Boundary | Version | Authority |
| --- | ---: | --- |
| Inspection Note Assistance | 1 | Python strict output + ASP.NET validation |
| Image Analysis Pass 1 | 1 | Python strict output |
| Final Inspection Image Analysis | 1 | Python strict output + ASP.NET validation |
| Reviewed Image Analysis projection | 1 | ASP.NET |
| Persisted Crop Field Analysis | 2 | ASP.NET deterministic wrapper |
| Member 3 crop-health consideration | 1 | ASP.NET validation |
| Member 4 scheduling/crop-health proposal | 2 | ASP.NET deterministic catalog |
| Farmer approved plan | 1 | ASP.NET server-composed projection |

Shared golden fixtures are under `docs/ai-usage/fixtures/member2` and are loaded by Python, C#, React, and Flutter tests. They intentionally cover valid, unknown-field/action, and unsupported-version payloads without introducing cross-language code generation.
