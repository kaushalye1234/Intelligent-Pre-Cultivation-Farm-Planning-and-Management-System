# Member 2 Crop Planning Handoff Contract

After Member 1 completes successfully, the next ready step is persisted as an `AgentStep` with:

```json
{
  "agentName": "CropFieldAnalysisAgent",
  "stepName": "FieldAnalysis",
  "sequence": 2,
  "status": "Pending"
}
```

Member 2 should consume the coordinator planning result from:

```http
GET /api/crop-plans/{cropPlanRequestId}/planning-result
```

Expected successful coordinator output:

```json
{
  "workflowId": "11111111-1111-1111-1111-111111111111",
  "status": "Planned",
  "requiresHumanReview": false,
  "warnings": [],
  "referenceDataStatus": "Available",
  "objectiveSummary": "Safe structured summary of the farmer objective.",
  "steps": [
    {
      "sequence": 1,
      "stepType": "FieldAnalysis",
      "assignedAgent": "CropFieldAnalysisAgent"
    },
    {
      "sequence": 2,
      "stepType": "WeatherResourceAnalysis",
      "assignedAgent": "WeatherResourceAgent"
    },
    {
      "sequence": 3,
      "stepType": "Scheduling",
      "assignedAgent": "SchedulingValidationAgent"
    }
  ]
}
```

Missing verified reference data:

```json
{
  "workflowId": "11111111-1111-1111-1111-111111111111",
  "status": "ReferenceDataUnavailable",
  "requiresHumanReview": true,
  "warnings": ["Verified crop reference data is missing."],
  "referenceDataStatus": "Unavailable",
  "objectiveSummary": "",
  "steps": []
}
```

Safe failure:

```json
{
  "workflowId": "11111111-1111-1111-1111-111111111111",
  "status": "SafeFailure",
  "requiresHumanReview": true,
  "warnings": ["LLM provider returned malformed JSON."],
  "referenceDataStatus": "Unknown",
  "objectiveSummary": "",
  "steps": []
}
```

Member 2 must not treat `requiresHumanReview` as final officer approval. Member 4 owns the later `requiresHumanApproval` execution gate.

## Optional pre-submit assistance (implemented 2026-10-03)

Two optional capabilities sit beside, not inside, the authoritative post-submission Field Analysis workflow:

- `InspectionNoteAssistantAgent` makes one text-only structured call for the six existing note fields. Its `contractVersion` is `1`, success status is `Available`, warnings are advisory, and Accept/Edit only change the local React form. Only Save Draft/Submit persists normal inspection text.
- `InspectionImageAnalysisAgent` analyzes one explicitly selected representative crop/leaf image. Its Pass 1 and final-result contract versions are `1`. One Analyze action permits one vision call, deterministic trusted-source retrieval, and one grounded call only. It never generates chemical actions.

The nested ASP.NET routes are:

```http
POST /api/crop-plans/{id}/pre-planting-assessment/note-suggestions
PUT  /api/crop-plans/{id}/pre-planting-assessment/representative-image/{imageId}
POST /api/crop-plans/{id}/pre-planting-assessment/image-analysis
GET  /api/crop-plans/{id}/pre-planting-assessment/image-analysis
POST /api/crop-plans/{id}/pre-planting-assessment/image-analysis/review
GET  /api/crop-plans/{id}/pre-planting-assessment/image-analysis/history
```

React never calls Python directly and never supplies an image URL, hash, fingerprint, analysis ID, review ID, crop identity, or workflow identity as trusted input. ASP.NET derives those values, authorizes the current inspection, and uses existing service/tool tokens for internal calls. New Cloudinary evidence uses authenticated delivery; browser viewing goes through the authorized ASP.NET content route. AI retrieval uses the dedicated backend tool and verifies stored asset identity, revision, MIME, size, and the original SHA-256 before returning bounded bytes. Python creates an in-memory, orientation-corrected, metadata-stripped derivative and sends only that derivative to OpenAI.

Image analysis records are Member 2 domain records, not `AgentWorkflow` state. Each fingerprinted `InspectionImageAnalysis` transitions once from `Running` to a terminal state and then remains immutable. Reviews are append-only and point to the exact immutable analysis. Submission locks the inspection row and freezes the exact eligible Accepted/Edited review ID, or null, in the same transaction as completion. Pending, unreviewed, rejected, failed, stale, interrupted, and superseded results never become downstream input.

The explicit history route is staff-only. The owning Field Officer may read their inspection history; authorized AO/Admin oversight is read-only. It includes immutable attempts, exact bounded evidence packets, raw results, failures, and append-only reviews. Resource Officers and farmers cannot access it. Normal downstream DTOs omit audit payloads.

Trusted grounding reuses `crop_finding_sources.json` and Member 1 URL/SSRF/redirect/fetch primitives. Member 2 does not call `CropFindingAgent` or OpenAI web search. Stage 1 is Sri Lanka-first and uses deterministic crop+issue relevance, snippet selection, novelty deduplication, coverage early-stop, and Stage 2 only when zero usable Stage 1 documents exist. At most three evidence documents and the exact bounded extracts sent to Pass 2 are persisted. The total operation budget is 120 seconds with no automatic retries.

The action vocabulary is fixed to `FieldSanitation`, `RemoveAffectedResidue`, `SeparateAffectedMaterial`, `InspectNearbyPlants`, `MonitorSymptoms`, `PrePlantingCleanup`, and `RequestFurtherAssessment`. Server-owned catalog wording is authoritative. Free-text recommendations, chemical products, active ingredients, dosages, application rates, and spray schedules cannot enter the downstream action collection.

## Implemented Member 2 Execution

The Field Officer records the request-specific assessment before Member 2 runs:

```http
GET /api/crop-plans/{cropPlanRequestId}/pre-planting-assessment
PUT /api/crop-plans/{cropPlanRequestId}/pre-planting-assessment
POST /api/inspections/{prePlantingInspectionId}/images
POST /api/crop-plans/{cropPlanRequestId}/pre-planting-assessment/submit
```

The assessment is stored as the single linked `FieldInspection` with `purpose = PrePlanting`; its structured values are stored in `InspectionObservation`. Draft values are nullable. In particular, missing `identifiedRisks` means risks have not been assessed, while an empty array means the officer explicitly assessed and found none. Submission reloads and validates the persisted observations, then makes the assessment immutable. Only the owning Field Officer may create, edit, attach evidence to, submit, run, or retry it. Agricultural Officers and Admins may read it. The field-analysis run is rejected until this exact linked inspection has been submitted.

Run Member 2 from ASP.NET:

```http
POST /api/crop-plans/{cropPlanRequestId}/run-field-analysis
```

Successful run response:

```json
{
  "workflowId": "11111111-1111-1111-1111-111111111111",
  "cropPlanRequestId": "22222222-2222-2222-2222-222222222222",
  "fieldAnalysisStepId": "33333333-3333-3333-3333-333333333333",
  "status": "Analyzed",
  "requiresHumanReview": true,
  "warnings": [
    "Inspection image metadata is available for human review; no AI visual analysis was performed."
  ]
}
```

Read persisted Member 2 output:

```http
GET /api/crop-plans/{cropPlanRequestId}/field-analysis-result
```

Persisted output shape:

```json
{
  "contractVersion": 2,
  "workflowId": "11111111-1111-1111-1111-111111111111",
  "status": "Analyzed",
  "requiresHumanReview": true,
  "warnings": [
    "Inspection image metadata is available for human review; no AI visual analysis was performed."
  ],
  "fieldCondition": {
    "summary": "Submitted pre-planting evidence records adequate canal water, good drainage, low waterlogging risk, and minor remaining land preparation.",
    "evidenceInspectionIds": ["44444444-4444-4444-4444-444444444444"]
  },
  "openIssues": [
    {
      "issueId": "55555555-5555-5555-5555-555555555555",
      "severity": "High",
      "status": "Open",
      "evidenceInspectionId": "44444444-4444-4444-4444-444444444444"
    }
  ],
  "priority": "High",
  "fieldSuitability": "SuitableWithConditions",
  "soilAssessment": "Soil type Loamy; condition Good; moisture Moist.",
  "waterAssessment": "Water availability Adequate; main source Canal; irrigation Available; reliability Reliable.",
  "drainageAssessment": "Drainage condition Good; waterlogging risk Low.",
  "fieldPreparationRequirements": [
    "Complete the recorded land preparation before planting."
  ],
  "plantingReadiness": "ReadyWithMinorPreparation",
  "identifiedRisks": ["LandPreparationRequired"],
  "recommendedPrePlantingActions": [
    "Resolve the exact linked field-access constraint before field operations begin."
  ]
}
```

When an eligible review was frozen at submission, the deterministic Field Analysis wrapper—not the model—also attaches `reviewedCropIssueActions` and `reviewedCropHealthGuidance`. The same attachment occurs after a successful validated deterministic Field Analysis fallback. SafeFailure or invalid Field Analysis never forwards those actions. The reviewed projection contract is version `1`; action order and Member 2 provenance are preserved.

`fieldSuitability` is one of `Suitable`, `SuitableWithConditions`, `NotSuitable`, `RequiresFurtherAssessment`, or `Unknown`. `plantingReadiness` uses the submitted assessment code, with `Unknown` reserved for safe-failure/insufficient-evidence output. Structured arrays are always present in newly persisted output. Existing `fieldCondition`, `openIssues`, and `priority` remain backward compatible.

Current submitted observations establish deterministic priority before exact linked issues are considered. `NotReady`, unavailable water, high waterlogging risk, a waterlogged field, or a supported severe soil/risk condition produce `High`. Preparation/further-assessment readiness, limited/seasonal water, poor drainage, moderate waterlogging, preparation/access/erosion concerns, or supported medium soil conditions produce `Medium`. Fully favorable required observations with no material structured risks produce `Low`. Exact linked issues may raise this result but never reduce or replace the observation-based result.

Member 2 calls the AI service through the shared `IAgenticAIClient` route:

```http
POST /workflows/crop-planning/field-analysis
```

AI-service input:

```json
{
  "workflowId": "11111111-1111-1111-1111-111111111111",
  "cropPlanRequestId": "22222222-2222-2222-2222-222222222222",
  "prePlantingInspectionId": "44444444-4444-4444-4444-444444444444",
  "fieldId": "66666666-6666-6666-6666-666666666666",
  "cropCycleId": "77777777-7777-7777-7777-777777777777",
  "requestedAnalysis": ["FieldCondition", "OpenIssues", "InspectionEvidence"],
  "cropReferenceProfileId": "88888888-8888-8888-8888-888888888888",
  "agentStepId": "33333333-3333-3333-3333-333333333333"
}
```

## Internal Tool Scope

`CropFieldAnalysisAgent` is evidence-linked and read-only. Every inspection, issue, and image lookup is scoped by `workflowId`, `cropPlanRequestId`, `prePlantingInspectionId`, and `fieldId`; ASP.NET rejects a mismatched or unsubmitted inspection. It uses only these ASP.NET internal tools:

- `GetFieldDetails`
- `GetCropPlanContext`
- `GetCropCycleDetails`
- `GetRecentInspections`
- `GetOpenCropIssues`
- `GetInspectionImageMetadata`
- `GetCropReferenceProfile`

The existing `CropFieldAnalysisAgent` image tool still returns evidence metadata only and performs no visual analysis. The separate pre-submit `InspectionImageAnalysisAgent` is the only Member 2 path permitted to receive verified bytes through its narrow authenticated tool. This preserves the existing post-submission Field Analysis semantics.

If internal tools fail, the LLM times out, output is malformed, required observations/risk state/reference data are missing, output conflicts with the exact submitted readiness/risks/priority, output references unknown inspection or issue IDs, or unsafe action/treatment language appears, Member 2 returns `SafeFailure` with `requiresHumanReview: true`.

A failed attempt marks only the `CropFieldAnalysisAgent` step as retryable `Failed`. The workflow stays non-terminal with `currentStep = CropFieldAnalysisAgent` and `completedAt = null`. Acquisition and completion each advance the workflow concurrency version. Only a validated and successfully persisted `Analyzed` result completes Member 2 and advances to `WeatherResourceAgent`; a completed run is idempotent and is not executed again.

## Member 3 Handoff

After Member 2 completes, ASP.NET marks the workflow pending for `WeatherResourceAgent` and exposes a safe read-only summary through the Crop Plan/workflow boundary:

```http
GET /api/crop-plans/{cropPlanRequestId}/member-3-handoff
```

Handoff response:

```json
{
  "contractVersion": 1,
  "workflowId": "11111111-1111-1111-1111-111111111111",
  "cropPlanRequestId": "22222222-2222-2222-2222-222222222222",
  "fieldId": "66666666-6666-6666-6666-666666666666",
  "cropCycleId": "77777777-7777-7777-7777-777777777777",
  "fieldLocationContext": "Farm: North Farm; Location: North; Field: Field A; Soil: Loam; Area: 2.00",
  "preferredStartDate": "2026-09-20",
  "preferredEndDate": "2026-10-20",
  "fieldAnalysisSummary": "Submitted pre-planting evidence records an exact field-access constraint and remaining land preparation.",
  "priority": "High",
  "warnings": [],
  "requiresHumanReview": true,
  "fieldSuitability": "SuitableWithConditions",
  "soilAssessment": "Soil type Loamy; condition Moderate; moisture Moist.",
  "waterAssessment": "Water availability Adequate; main source Canal; irrigation Available; reliability Reliable.",
  "drainageAssessment": "Drainage condition Poor; waterlogging risk Moderate.",
  "fieldPreparationRequirements": [
    "Clear the recorded drainage channels before planting."
  ],
  "plantingReadiness": "RequiresPreparation",
  "identifiedRisks": ["PoorDrainage"],
  "recommendedPrePlantingActions": [
    "Address the recorded drainage concern before planting."
  ]
}
```

The handoff may add read-only `reviewedCropIssueActions`. Member 3 may return separate version-1 action-linked weather/resource considerations, but it cannot echo as authority, rewrite, remove, approve, reject, reclassify, or strengthen the original actions.

## Shared compatibility fixtures

Native C#, Python, React, and Flutter models are aligned without code generation. Shared valid/invalid fixtures live in `docs/ai-usage/fixtures/member2`. Unknown action identifiers, unsupported versions, and Python extra fields are rejected explicitly. The ASP.NET action enum/catalog owns downstream semantics and farmer wording; Python mirrors only the stable identifiers needed for strict structured output.

The route is available to Field Officer, Agricultural Officer, Admin, and Resource Officer only when the exact latest workflow has a completed `CropFieldAnalysisAgent` step and `currentStep = WeatherResourceAgent`. Farmer access is denied. Resource Officers remain denied from the full `/field-analysis-result` and every raw/generic PrePlanting inspection route.

This safe response never contains `OfficerNotes`, raw observation rows, risk notes, inspection image metadata, evidence inspection IDs, crop-issue IDs, or generic inspection history. Water, drainage, readiness, risk, and preparation values reach Member 3 only through this completed analysis result; Resource Officers do not re-enter or reassess Member 2 observations.
