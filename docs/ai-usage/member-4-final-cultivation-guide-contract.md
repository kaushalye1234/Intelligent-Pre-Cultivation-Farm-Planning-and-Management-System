# Member 4 Final Cultivation Guide Contract

`FinalCultivationGuideAgent` is a post-approval presentation agent. It runs only after the existing Member 4 approval transaction has committed approved tasks, irrigation schedules, reservations, the workflow decision, and the completed workflow state.

It does not replace `SchedulingValidationAgent`, repeat approval, or create operational records. ASP.NET Core remains authoritative and adds the exact approved activities to the final response after validating the AI narrative.

## Generation flow

```text
Member 1-4 persisted outputs
        -> Agricultural Officer/Admin approval transaction commits
        -> ASP.NET builds a normalized approved-revision input
        -> POST /workflows/crop-planning/final-cultivation-guide
        -> FinalCultivationGuideAgent returns narrative-only JSON
        -> ASP.NET validates workflow ID, revision, contract version,
           month window, and absence of numeric advice
        -> validated output is stored in a revision-specific AgentStep
        -> existing GET /api/task-approval/workflows/{workflowId}
           returns the guide step to the authorized farmer UI
```

The AI-service route requires the existing internal service token and uses the existing server-side `AI_PROVIDER`, `AI_MODEL`, and `OPENAI_API_KEY` configuration. Flutter never calls the AI service or OpenAI.

## Input boundary

The AI receives:

- workflow ID, crop-plan request ID, and approved candidate revision;
- crop, variety, farm, field, and location names where available;
- preferred cultivation start/end dates and the current UTC date;
- bounded summaries from completed Member 1-3 outputs;
- exact approved task, irrigation, and reservation identifiers and values.

The repository currently persists preferred start/end dates, not confirmed planting and harvest dates. The farmer UI labels them as preferred dates. The agent must not present them as confirmed operational dates.

Raw database entities, credentials, full prompts, provider responses, and unrelated personal data are not included.

## Output boundary

The AI may produce:

- practical guidance for the current week;
- an optional stage explanation only when supported by supplied evidence;
- month summaries inside the preferred cultivation window;
- field and weather precautions;
- supported risks;
- general harvest preparation;
- a short explanation of the evidence used.

Narrative fields cannot contain digits. This deliberately prevents the model from introducing dates, doses, quantities, rates, durations, or other high-impact numeric instructions. Exact approved activities are attached by ASP.NET after validation and include stable IDs, dates, reservation quantities/units, and irrigation durations.

## Persistence and stale-revision protection

The guide uses the existing `AgentStep` model:

- `AgentName = FinalCultivationGuideAgent`
- `StepName = FarmerGuide`
- `CandidateRevision = approved revision`
- `Pending/Running/Completed/Failed` lifecycle through `AgentStepStatus`

A filtered PostgreSQL unique index allows only one non-deleted guide step per workflow/revision. A completed valid step is returned on retry without another provider call. Only a guide whose workflow ID and approved revision match the current completed workflow is shown by Flutter.

## Safe failure

Provider, schema, validation, or persistence failures set the guide step to `Failed` when possible and return `Unavailable`. They do not change the approved workflow, tasks, irrigation schedules, reservations, or approval decision. Flutter continues to show the existing approved records and explains that enriched guidance is temporarily unavailable.

Agricultural Officers and Admins may retry generation through:

```http
POST /api/task-approval/workflows/{workflowId}/generate-final-guide?approvedRevision={revision}
```

The existing workflow detail endpoint remains farmer ownership-scoped and backward compatible with workflows that have no guide step.
