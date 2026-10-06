import json
from uuid import UUID

import pytest

from agents.final_cultivation_guide_agent import FinalCultivationGuideAgent
from providers.base_llm_provider import LLMResponse
from schemas.final_cultivation_guide import FinalCultivationGuideInput


WORKFLOW_ID = UUID("11111111-1111-1111-1111-111111111111")


class FakeProvider:
    def __init__(self, payload):
        self.payload = payload
        self.prompt = None

    async def generate_json(self, prompt, response_schema=None):
        self.prompt = prompt
        return LLMResponse(text=json.dumps(self.payload))


def guide_payload(workflow_id=WORKFLOW_ID, revision=2):
    return {
        "contractVersion": 1,
        "workflowId": str(workflow_id),
        "approvedRevision": revision,
        "weeklyGuidance": ["Check the field for standing water."],
        "currentStageExplanation": "The crop is in its early growth stage.",
        "monthlyGuidance": [{
            "month": "2026-11",
            "summary": "Support healthy early growth.",
            "fieldAdvice": ["Keep drainage channels clear."],
            "weatherAdvice": ["Monitor rainfall before field work."],
        }],
        "risks": ["Standing water may affect the field."],
        "harvestPreparation": ["Keep the harvest area accessible."],
        "whyThisPlan": "This guide uses the approved crop, field, weather, and schedule information.",
    }


def guide_input():
    return FinalCultivationGuideInput.model_validate({
        "contractVersion": 1,
        "workflowId": str(WORKFLOW_ID),
        "approvedRevision": 2,
        "cropPlanRequestId": "22222222-2222-2222-2222-222222222222",
        "cropName": "Maize",
        "varietyName": "Local variety",
        "farmName": "Farm A",
        "fieldName": "Field A",
        "location": "Kurunegala",
        "preferredStartDate": "2026-10-20",
        "preferredEndDate": "2027-02-15",
        "currentDate": "2026-10-20",
        "evidenceSummary": ["Officer field assessment reports clear drainage."],
        "approvedActivities": [{
            "id": "33333333-3333-3333-3333-333333333333",
            "kind": "FarmTask",
            "title": "Prepare the field",
            "scheduledAt": "2026-10-19T08:00:00Z",
        }],
    })


@pytest.mark.asyncio
async def test_guide_returns_narrative_without_repeating_authoritative_activity_values():
    provider = FakeProvider(guide_payload())

    result = await FinalCultivationGuideAgent(provider).run(guide_input())

    assert result.workflow_id == WORKFLOW_ID
    assert result.approved_revision == 2
    assert result.monthly_guidance[0].month == "2026-11"
    assert "approvedActivities" not in result.model_dump(by_alias=True)
    assert "must not propose numeric quantities or alter approved activities" in provider.prompt


@pytest.mark.asyncio
async def test_guide_rejects_output_for_a_different_approved_revision():
    provider = FakeProvider(guide_payload(revision=1))

    with pytest.raises(ValueError, match="workflow or revision"):
        await FinalCultivationGuideAgent(provider).run(guide_input())


@pytest.mark.asyncio
async def test_guide_rejects_numeric_advice_that_could_override_approved_values():
    payload = guide_payload()
    payload["weeklyGuidance"] = ["Apply 25 kilograms of fertilizer."]

    with pytest.raises(ValueError, match="numeric guidance"):
        await FinalCultivationGuideAgent(FakeProvider(payload)).run(guide_input())
