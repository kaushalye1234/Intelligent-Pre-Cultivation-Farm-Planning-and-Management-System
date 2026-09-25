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

The image tool returns Cloudinary evidence metadata only: image ID, inspection ID, URL, public ID, content type, size, and created date. The agent must not receive image bytes, Cloudinary assets, or base64 payloads, and it must not perform visual analysis. Uploaded images remain human-review evidence.

If internal tools fail, the LLM times out, output is malformed, required observations/risk state/reference data are missing, output conflicts with the exact submitted readiness/risks/priority, output references unknown inspection or issue IDs, or unsafe action/treatment language appears, Member 2 returns `SafeFailure` with `requiresHumanReview: true`.

A failed attempt marks only the `CropFieldAnalysisAgent` step as retryable `Failed`. The workflow stays non-terminal with `currentStep = CropFieldAnalysisAgent` and `completedAt = null`. Acquisition and completion each advance the workflow concurrency version. Only a validated and successfully persisted `Analyzed` result completes Member 2 and advances to `WeatherResourceAgent`; a completed run is idempotent and is not executed again.

## Member 3 Handoff

After Member 2 completes, ASP.NET marks the workflow pending for `WeatherResourceAgent` and exposes the Member 3 handoff:

```http
GET /api/crop-plans/{cropPlanRequestId}/member-3-handoff
```

Handoff response:

```json
{
  "workflowId": "11111111-1111-1111-1111-111111111111",
  "cropPlanRequestId": "22222222-2222-2222-2222-222222222222",
  "fieldId": "66666666-6666-6666-6666-666666666666",
  "cropCycleId": "77777777-7777-7777-7777-777777777777",
  "fieldLocationContext": "Farm: North Farm; Location: North; Field: Field A; Soil: Loam; Area: 2.00",
  "preferredStartDate": "2026-09-20",
  "preferredEndDate": "2026-10-20",
  "fieldAnalysisSummary": "Submitted pre-planting evidence records an exact field-access constraint and remaining land preparation.",
  "priority": "High",
  "evidenceInspectionIds": ["44444444-4444-4444-4444-444444444444"],
  "openIssues": [
    {
      "issueId": "55555555-5555-5555-5555-555555555555",
      "severity": "High",
      "status": "Open",
      "evidenceInspectionId": "44444444-4444-4444-4444-444444444444"
    }
  ],
  "warnings": []
}
```
