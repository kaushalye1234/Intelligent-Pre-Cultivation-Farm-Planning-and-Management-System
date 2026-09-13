from fastapi import Depends, FastAPI

from agents.crop_field_analysis_agent import CropFieldAnalysisAgent
from agents.crop_planning_coordinator_agent import CropPlanningCoordinatorAgent
from auth import require_service_token
from config import Settings, get_settings
from graph.workflow_graph import build_crop_planning_graph, build_field_analysis_graph
from providers import create_provider
from schemas.crop_planning import CoordinatorInput, CropPlanningCoordinatorOutput
from schemas.field_analysis import CropFieldAnalysisOutput, FieldAnalysisInput
from tools.backend_tool_client import BackendToolClient
from tools.crop_planning_tools import CropPlanningTools
from tools.inspection_tools import InspectionTools

app = FastAPI(title="AgriAssist AI Service", version="0.1.0")


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post(
    "/workflows/crop-planning/coordinator",
    response_model=CropPlanningCoordinatorOutput,
    dependencies=[Depends(require_service_token)],
)
async def run_crop_planning_coordinator(
    request: CoordinatorInput,
    settings: Settings = Depends(get_settings),
) -> CropPlanningCoordinatorOutput:
    backend_client = BackendToolClient(settings)
    tools = CropPlanningTools(backend_client)
    provider = create_provider(settings)
    agent = CropPlanningCoordinatorAgent(
        tools=tools,
        llm_provider=provider,
        provider_timeout_seconds=settings.provider_timeout_seconds,
    )
    graph = build_crop_planning_graph(agent)
    state = await graph.ainvoke({"request": request, "output": None})
    return state["output"]


@app.post(
    "/workflows/crop-planning/field-analysis",
    response_model=CropFieldAnalysisOutput,
    dependencies=[Depends(require_service_token)],
)
async def run_crop_field_analysis(
    request: FieldAnalysisInput,
    settings: Settings = Depends(get_settings),
) -> CropFieldAnalysisOutput:
    backend_client = BackendToolClient(settings)
    tools = InspectionTools(backend_client)
    provider = create_provider(settings)
    agent = CropFieldAnalysisAgent(
        tools=tools,
        llm_provider=provider,
        provider_timeout_seconds=settings.provider_timeout_seconds,
    )
    graph = build_field_analysis_graph(agent)
    state = await graph.ainvoke({"request": request, "output": None})
    return state["output"]

