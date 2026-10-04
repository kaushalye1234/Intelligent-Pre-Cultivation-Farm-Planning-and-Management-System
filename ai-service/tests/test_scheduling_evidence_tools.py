from datetime import date, datetime, timedelta, timezone
from uuid import uuid4

import pytest

from schemas.crop_planning import CropPlanContext, CropReferenceProfile
from test_scheduling_validation_agent import sourced_request
from tools.scheduling_evidence_tools import SchedulingEvidenceTools


def crop_context(request):
    crop_type_id = uuid4()
    return CropPlanContext(
        id=request.crop_plan_request_id, farmId=request.farm_id, fieldId=request.field_id,
        cropTypeId=crop_type_id, requestedByUserId=request.assigned_to_user_id,
        preferredStartDate=date.today(), preferredEndDate=date.today(), budget=50,
        objective="planning", status="Submitted", farm={"id": str(request.farm_id)},
        cropType={"id": str(crop_type_id), "name": "Crop", "isActive": True},
        cropVarietyName="Variety",
    )


@pytest.mark.asyncio
async def test_profile_tool_uses_only_existing_typed_get_wrappers():
    request = sourced_request()
    context = crop_context(request)
    context.crop_variety_name = "Variety"
    profile = {
        "id": str(uuid4()), "cropTypeId": str(context.crop_type_id), "varietyName": "Variety",
        "sourceName": "Extension guide", "sourceUrl": "https://example.test/guide",
        "sourceVersion": "v2", "verifiedAt": datetime.now(timezone.utc).isoformat(), "isActive": True,
    }
    reference = CropReferenceProfile(referenceDataStatus="Available", profile=profile, stages=[{
        "id": str(uuid4()), "stageName": "Planting", "sequence": 1, "typicalMinDays": 3,
        "typicalMaxDays": 5, "sourceName": "Extension guide",
    }])

    class FakeCropPlanningTools:
        async def get_crop_plan_context(self, crop_plan_request_id, workflow_id):
            assert crop_plan_request_id == request.crop_plan_request_id
            assert workflow_id == request.workflow_id
            return context

        async def get_crop_reference_profile(self, crop_type_id, workflow_id, variety_name):
            assert crop_type_id == context.crop_type_id
            assert workflow_id == request.workflow_id
            assert variety_name == "Variety"
            return reference

    evidence = await SchedulingEvidenceTools(FakeCropPlanningTools()).get_verified_crop_profile(request)
    assert str(evidence.profile_id) == profile["id"]
    assert evidence.source_version == "v2"
    assert evidence.stages[0].stage_name == "Planting"
    assert evidence.coordinator_step_id == request.evidence.coordinator_step_id


@pytest.mark.asyncio
@pytest.mark.parametrize("status,active,stages", [
    ("Unavailable", True, [{"id": str(uuid4()), "stageName": "Planting", "sequence": 1, "sourceName": "x"}]),
    ("Available", False, [{"id": str(uuid4()), "stageName": "Planting", "sequence": 1, "sourceName": "x"}]),
    ("Available", True, []),
])
async def test_profile_tool_rejects_unavailable_inactive_or_stage_less_profile(status, active, stages):
    request = sourced_request()
    context = crop_context(request)
    reference = CropReferenceProfile(referenceDataStatus=status, profile={
        "id": str(uuid4()), "cropTypeId": str(context.crop_type_id), "varietyName": None,
        "sourceName": "source", "sourceVersion": "v1",
        "verifiedAt": datetime.now(timezone.utc).isoformat(), "isActive": active,
    } if status == "Available" else None, stages=stages)

    class FakeCropPlanningTools:
        async def get_crop_plan_context(self, *args):
            return context

        async def get_crop_reference_profile(self, *args):
            return reference

    with pytest.raises(ValueError):
        await SchedulingEvidenceTools(FakeCropPlanningTools()).get_verified_crop_profile(request)


@pytest.mark.asyncio
@pytest.mark.parametrize("invalidity", ["crop", "variety", "future_timestamp"])
async def test_profile_tool_rejects_crop_variety_or_future_verification(invalidity):
    request = sourced_request()
    context = crop_context(request)
    reference_time = datetime.now(timezone.utc)
    profile = {
        "id": str(uuid4()),
        "cropTypeId": str(uuid4() if invalidity == "crop" else context.crop_type_id),
        "varietyName": "Different" if invalidity == "variety" else "Variety",
        "sourceName": "Verified guide",
        "sourceVersion": "v1",
        "verifiedAt": (reference_time + timedelta(days=1) if invalidity == "future_timestamp" else reference_time).isoformat(),
        "isActive": True,
    }
    reference = CropReferenceProfile(referenceDataStatus="Available", profile=profile, stages=[{
        "id": str(uuid4()), "stageName": "Planting", "sequence": 1, "sourceName": "Guide",
    }])

    class FakeCropPlanningTools:
        async def get_crop_plan_context(self, *args):
            return context

        async def get_crop_reference_profile(self, *args):
            return reference

    with pytest.raises(ValueError):
        await SchedulingEvidenceTools(FakeCropPlanningTools()).get_verified_crop_profile(request)


@pytest.mark.asyncio
async def test_profile_tool_rejects_oversized_crop_context_before_profile_fetch():
    request = sourced_request()
    context = crop_context(request).model_copy(update={"objective": "x" * 17000})

    class FakeCropPlanningTools:
        profile_calls = 0

        async def get_crop_plan_context(self, *args):
            return context

        async def get_crop_reference_profile(self, *args):
            self.profile_calls += 1
            raise AssertionError("Oversized context must stop before profile retrieval.")

    tools = FakeCropPlanningTools()
    with pytest.raises(ValueError, match="size limit"):
        await SchedulingEvidenceTools(tools).get_verified_crop_profile(request)
    assert tools.profile_calls == 0
