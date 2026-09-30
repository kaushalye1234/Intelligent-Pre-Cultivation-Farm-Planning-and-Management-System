import pytest

from agents.scheduling_validation_agent import SchedulingValidationAgent
from graph.workflow_graph import build_scheduling_validation_graph
from test_scheduling_validation_agent import request, sourced_request


@pytest.mark.asyncio
async def test_graph_skips_candidate_generation_when_evidence_is_missing():
    agent = SchedulingValidationAgent()
    called = False
    original = agent.propose

    def tracked(value):
        nonlocal called
        called = True
        return original(value)

    agent.propose = tracked
    graph = build_scheduling_validation_graph(agent)
    result = await graph.ainvoke({"request": request(), "output": None})

    assert result["output"].status == "MissingDependency"
    assert called is False


@pytest.mark.asyncio
async def test_graph_builds_sourced_candidate_then_applies_weather_risk():
    graph = build_scheduling_validation_graph(SchedulingValidationAgent())
    value = sourced_request()
    value.weather_resource_output["weatherRisk"] = "High"
    result = await graph.ainvoke({"request": value, "output": None})

    assert {"validate_evidence", "build_candidate", "assess_risk"}.issubset(graph.get_graph().nodes)
    assert result["output"].status == "CandidateBlocked"
    assert result["output"].requires_human_approval is False
    assert result["output"].candidate_tasks[0].sources
