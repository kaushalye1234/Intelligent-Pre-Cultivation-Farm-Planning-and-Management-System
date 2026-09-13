from typing import TypedDict

from langgraph.graph import END, StateGraph

from agents.crop_field_analysis_agent import CropFieldAnalysisAgent
from agents.crop_planning_coordinator_agent import CropPlanningCoordinatorAgent
from schemas.crop_planning import CoordinatorInput, CropPlanningCoordinatorOutput
from schemas.field_analysis import CropFieldAnalysisOutput, FieldAnalysisInput


class CoordinatorState(TypedDict):
    request: CoordinatorInput
    output: CropPlanningCoordinatorOutput | None


class FieldAnalysisState(TypedDict):
    request: FieldAnalysisInput
    output: CropFieldAnalysisOutput | None


def build_crop_planning_graph(agent: CropPlanningCoordinatorAgent):
    async def run_coordinator(state: CoordinatorState) -> CoordinatorState:
        output = await agent.run(state["request"])
        return {"request": state["request"], "output": output}

    graph = StateGraph(CoordinatorState)
    graph.add_node("crop_planning_coordinator", run_coordinator)
    graph.set_entry_point("crop_planning_coordinator")
    graph.add_edge("crop_planning_coordinator", END)
    return graph.compile()


def build_field_analysis_graph(agent: CropFieldAnalysisAgent):
    async def run_field_analysis(state: FieldAnalysisState) -> FieldAnalysisState:
        output = await agent.run(state["request"])
        return {"request": state["request"], "output": output}

    graph = StateGraph(FieldAnalysisState)
    graph.add_node("field_analysis", run_field_analysis)
    graph.set_entry_point("field_analysis")
    graph.add_edge("field_analysis", END)
    return graph.compile()
