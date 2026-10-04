from datetime import datetime, timezone
from types import SimpleNamespace
from uuid import uuid4

import pytest

from agents.scheduling_profile_retriever import SchedulingProfileRetriever
from config import Settings
from providers.base_llm_provider import LLMFunctionCall, LLMToolCallResult
from schemas.scheduling_validation import SchedulingEvidenceBundle
from test_scheduling_validation_agent import request, sourced_request


class FakeProvider:
    def __init__(self, result=None):
        self.calls = 0
        self.result = result or LLMToolCallResult(LLMFunctionCall("call-1", "GetVerifiedCropProfile", {}))

    async def generate_tool_call(self, prompt, tool_schema):
        self.calls += 1
        assert tool_schema["name"] == "GetVerifiedCropProfile"
        return self.result


class FakeEvidenceTools:
    def __init__(self, evidence):
        self.calls = 0
        self.evidence = evidence

    async def get_verified_crop_profile(self, request):
        self.calls += 1
        return self.evidence


def missing_profile_request():
    value = sourced_request()
    value.evidence = value.evidence.model_copy(update={"profile_id": None, "stages": []})
    value.weather_resource_output["requirementSource"] = {}
    return value


def retrieved_evidence(request_value):
    return SchedulingEvidenceBundle(
        profileId=uuid4(), sourceName="Verified source", sourceVersion="v1",
        verifiedAt=datetime.now(timezone.utc),
        coordinatorStepId=request_value.evidence.coordinator_step_id,
        fieldAnalysisStepId=request_value.evidence.field_analysis_step_id,
        weatherResourceStepId=request_value.evidence.weather_resource_step_id,
        stages=[{"id": uuid4(), "stageName": "Planting", "sequence": 1,
                 "typicalMinDays": 2, "typicalMaxDays": 4, "sourceName": "Verified source"}],
    )


@pytest.mark.asyncio
async def test_complete_profile_skips_provider_and_tool():
    provider, tools = FakeProvider(), FakeEvidenceTools(None)
    original = sourced_request()
    result = await SchedulingProfileRetriever(provider, tools).retrieve_profile(original)
    assert result is original
    assert provider.calls == tools.calls == 0


@pytest.mark.asyncio
async def test_missing_upstream_result_skips_provider_and_tool():
    provider, tools = FakeProvider(), FakeEvidenceTools(None)
    original = missing_profile_request()
    original.coordinator_output["status"] = "SafeFailure"
    result = await SchedulingProfileRetriever(provider, tools).retrieve_profile(original)
    assert result is original
    assert provider.calls == tools.calls == 0


@pytest.mark.asyncio
async def test_matching_profile_fills_only_missing_profile_evidence():
    original = missing_profile_request()
    evidence = retrieved_evidence(original)
    provider, tools = FakeProvider(), FakeEvidenceTools(evidence)
    result = await SchedulingProfileRetriever(provider, tools).retrieve_profile(original)
    assert provider.calls == tools.calls == 1
    assert result.evidence.profile_id == evidence.profile_id
    assert result.evidence.stages == evidence.stages
    assert result.workflow_id == original.workflow_id
    assert result.candidate_revision == original.candidate_revision
    assert result.coordinator_output == original.coordinator_output


@pytest.mark.asyncio
async def test_member3_profile_mismatch_keeps_original_evidence():
    original = missing_profile_request()
    original.weather_resource_output["requirementSource"] = {"cropReferenceProfileId": str(uuid4())}
    provider, tools = FakeProvider(), FakeEvidenceTools(retrieved_evidence(original))
    result = await SchedulingProfileRetriever(provider, tools).retrieve_profile(original)
    assert provider.calls == 1
    assert tools.calls == 1
    assert result is original


@pytest.mark.asyncio
async def test_model_cannot_select_an_unknown_or_parameterized_tool():
    original = missing_profile_request()
    provider = FakeProvider(LLMToolCallResult(LLMFunctionCall("call-1", "GetVerifiedCropProfile", {"workflowId": "attacker"})))
    tools = FakeEvidenceTools(retrieved_evidence(original))
    result = await SchedulingProfileRetriever(provider, tools).retrieve_profile(original)
    assert result is original
    assert provider.calls == 1
    assert tools.calls == 0


@pytest.mark.asyncio
async def test_provider_terminal_answer_and_backend_error_fail_closed():
    original = missing_profile_request()
    terminal = FakeProvider(LLMToolCallResult(None, "I cannot verify a profile."))
    tools = FakeEvidenceTools(retrieved_evidence(original))
    result = await SchedulingProfileRetriever(terminal, tools).retrieve_profile(original)
    assert result is original
    assert terminal.calls == 1
    assert tools.calls == 0

    class FailingTools(FakeEvidenceTools):
        async def get_verified_crop_profile(self, request):
            self.calls += 1
            raise TimeoutError("backend details must not escape")

    provider, failing_tools = FakeProvider(), FailingTools(None)
    result = await SchedulingProfileRetriever(provider, failing_tools).retrieve_profile(original)
    assert result is original
    assert provider.calls == failing_tools.calls == 1


@pytest.mark.asyncio
async def test_timeout_fails_closed(monkeypatch):
    import agents.scheduling_profile_retriever as module

    class SlowProvider(FakeProvider):
        async def generate_tool_call(self, prompt, tool_schema):
            self.calls += 1
            await __import__("asyncio").sleep(0.02)
            return self.result

    monkeypatch.setattr(module, "OVERALL_TIMEOUT_SECONDS", 0.001)
    original = missing_profile_request()
    provider, tools = SlowProvider(), FakeEvidenceTools(retrieved_evidence(original))
    result = await SchedulingProfileRetriever(provider, tools).retrieve_profile(original)
    assert result is original
    assert tools.calls == 0


def test_profile_retrieval_setting_defaults_off():
    assert Settings(_env_file=None).scheduling_profile_retrieval_enabled is False
