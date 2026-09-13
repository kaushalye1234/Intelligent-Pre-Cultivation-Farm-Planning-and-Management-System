from typing import TypedDict

from langgraph.graph import END, StateGraph

from agents.crop_planning_coordinator_agent import CropPlanningCoordinatorAgent
from schemas.crop_planning import CoordinatorInput, CropPlanningCoordinatorOutput


class CoordinatorState(TypedDict):
    request: CoordinatorInput
    output: CropPlanningCoordinatorOutput | None


def build_crop_planning_graph(agent: CropPlanningCoordinatorAgent):
    async def run_coordinator(state: CoordinatorState) -> CoordinatorState:
        output = await agent.run(state["request"])
        return {"request": state["request"], "output": output}

    graph = StateGraph(CoordinatorState)
    graph.add_node("crop_planning_coordinator", run_coordinator)
    graph.set_entry_point("crop_planning_coordinator")
    graph.add_edge("crop_planning_coordinator", END)
    return graph.compile()
