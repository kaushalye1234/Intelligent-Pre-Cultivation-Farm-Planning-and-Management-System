from datetime import date, datetime, timezone
from uuid import uuid4

import pytest

from agents.scheduling_validation_agent import SchedulingValidationAgent
from schemas.scheduling_validation import ExistingFarmTask, SchedulingValidationInput


def request(**overrides) -> SchedulingValidationInput:
    workflow_id = uuid4()
    values = {
        "workflowId": workflow_id,
        "candidateRevision": 1,
        "cropPlanRequestId": uuid4(),
        "farmId": uuid4(),
        "fieldId": uuid4(),
        "assignedToUserId": uuid4(),
        "preferredStartDate": date(2026, 10, 1),
        "preferredEndDate": date(2026, 10, 8),
        "budget": 12000,
        "coordinatorOutput": {"workflowId": str(workflow_id), "status": "Planned"},
        "fieldAnalysisOutput": {"workflowId": str(workflow_id), "status": "Analyzed", "priority": "High", "warnings": []},
        "weatherResourceOutput": {"workflowId": str(workflow_id), "status": "Analyzed", "weatherRisk": "Medium", "warnings": []},
        "existingTasks": [],
        "existingIrrigation": [],
    }
    values.update(overrides)
    return SchedulingValidationInput.model_validate(values)


@pytest.mark.asyncio
async def test_builds_deterministic_candidate_that_requires_approval():
    value = request()

    result = await SchedulingValidationAgent().run(value)

    assert result.status == "CandidateReady"
    assert result.requires_human_approval is True
    assert len(result.candidate_tasks) == 1
    assert len(result.candidate_irrigation) == 1
    assert result.estimated_cost is None


@pytest.mark.asyncio
async def test_missing_upstream_output_never_creates_candidates():
    value = request(fieldAnalysisOutput={"status": "SafeFailure"})

    result = await SchedulingValidationAgent().run(value)

    assert result.status == "MissingDependency"
    assert result.requires_human_review is True
    assert result.requires_human_approval is False
    assert result.candidate_tasks == []
    assert result.candidate_irrigation == []


@pytest.mark.asyncio
async def test_moves_candidate_away_from_existing_conflicts():
    value = request()
    value.existing_tasks = [ExistingFarmTask(
        id=uuid4(),
        assignedToUserId=value.assigned_to_user_id,
        dueAt=datetime(2026, 10, 1, 8, tzinfo=timezone.utc),
        status=2,
    )]

    result = await SchedulingValidationAgent().run(value)

    assert result.candidate_tasks[0].due_at.hour == 9
