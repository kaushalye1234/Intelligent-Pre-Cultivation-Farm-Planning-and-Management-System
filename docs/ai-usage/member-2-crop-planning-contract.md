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
