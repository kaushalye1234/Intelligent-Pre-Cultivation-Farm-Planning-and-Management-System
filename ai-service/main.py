from fastapi import Depends, FastAPI

import asyncio
import logging
import time
from typing import Awaitable, Callable, TypeVar
from uuid import uuid4

from fastapi import HTTPException

from agents.crop_field_analysis_agent import CropFieldAnalysisAgent
from agents.crop_finding_agent import CropFindingAgent
from agents.crop_planning_coordinator_agent import CropPlanningCoordinatorAgent
from agents.inspection_note_assistant_agent import InspectionNoteAssistantAgent
from agents.inspection_image_analysis_agent import InspectionImageAnalysisAgent
from agents.weather_resource_agent import WeatherResourceAgent
from agents.scheduling_validation_agent import SchedulingValidationAgent
from agents.scheduling_profile_retriever import SchedulingProfileRetriever
from auth import require_service_token
from config import Settings, get_settings
from graph.workflow_graph import build_crop_planning_graph, build_field_analysis_graph, build_scheduling_validation_graph, build_weather_resource_graph
from providers import create_crop_finding_provider, create_provider
from providers.base_llm_provider import LLMProviderError, ProviderConfigurationError
from schemas.crop_finding import (
    CropFindingErrorDetail,
    CropSuggestionsResponse,
    DiscoverReferencesInput,
    ReferenceDiscoveryResponse,
    SuggestCropsInput,
    SuggestVarietiesInput,
    VarietySuggestionsResponse,
)
from schemas.crop_planning import CoordinatorInput, CropPlanningCoordinatorOutput
from schemas.field_analysis import CropFieldAnalysisOutput, FieldAnalysisInput
from schemas.inspection_note_assistance import InspectionNoteAssistanceInput, InspectionNoteAssistanceOutput
from schemas.inspection_image_analysis import (
    IMAGE_PREPROCESSING_VERSION,
    INSPECTION_IMAGE_ANALYSIS_CONTRACT_VERSION,
    INSPECTION_IMAGE_PROMPT_CONTRACT_VERSION,
    InspectionImageAnalysisCapabilityResponse,
    InspectionImageAnalysisInput,
    InspectionImageAnalysisOperationResponse,
)
from schemas.weather_resource import WeatherResourceInput, WeatherResourceOutput
from schemas.scheduling_validation import SchedulingValidationInput, SchedulingValidationOutput
from tools.backend_tool_client import BackendToolClient
from tools.crop_planning_tools import CropPlanningTools
from tools.scheduling_evidence_tools import SchedulingEvidenceTools
from tools.crop_finding_tools import CropFindingTools
from tools.inspection_tools import InspectionTools
from tools.inspection_image_analysis_tools import InspectionImageAnalysisTools
from tools.crop_health_evidence_adapter import CropHealthEvidenceAdapter, RELEVANCE_RULE_VERSION
from tools.crop_finding_tools import CropFindingTools, SourcePolicy
from tools.weather_resource_tools import WeatherResourceTools

app = FastAPI(title="AgriAssist AI Service", version="0.1.0")
logger = logging.getLogger("agriassist.crop_finding")
T = TypeVar("T")


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}


async def _run_crop_finding(
    *,
    action: str,
    admin_user_id: str,
    crop: str | None,
    variety: str | None,
    settings: Settings,
    operation: Callable[[CropFindingAgent], Awaitable[T]],
) -> T:
    started = time.monotonic()
    request_id = str(uuid4())
    agent: CropFindingAgent | None = None
    logger.info(
        "CropFinding request started requestId=%s action=%s adminUserId=%s crop=%s variety=%s",
        request_id,
        action,
        admin_user_id,
        crop,
        variety,
    )
    try:
        agent = CropFindingAgent(
            CropFindingTools(settings),
            create_crop_finding_provider(settings),
            request_id=request_id,
            operation_started_at=started,
        )
        result = await asyncio.wait_for(operation(agent), timeout=settings.crop_finding_overall_timeout_seconds)
    except ProviderConfigurationError as exc:
        logger.warning(
            "CropFinding configuration failure requestId=%s action=%s adminUserId=%s error=%s",
            request_id,
            action,
            admin_user_id,
            type(exc).__name__,
        )
        detail = CropFindingErrorDetail(
            code="CROP_FINDING_CONFIGURATION_UNAVAILABLE",
            message="CropFinding OpenAI configuration is unavailable.",
            requestId=request_id,
            operation=agent.active_operation if agent else None,
            stage=agent.active_stage if agent else None,
            attempt=agent.active_attempt if agent else None,
            category="configuration",
        )
        raise HTTPException(
            status_code=503,
            detail=detail.model_dump(mode="json", by_alias=True, exclude_none=True),
        ) from exc
    except asyncio.TimeoutError as exc:
        operation_name = agent.active_operation if agent else None
        stage = agent.active_stage if agent else None
        attempt = agent.active_attempt if agent else None
        logger.warning(
            "CropFinding timeout requestId=%s action=%s adminUserId=%s operation=%s stage=%s attempt=%s "
            "exceptionClass=%s rootCauseClass=%s category=timeout safeMessage=%s",
            request_id,
            action,
            admin_user_id,
            operation_name,
            stage,
            attempt,
            type(exc).__name__,
            type(exc.__cause__).__name__ if exc.__cause__ else type(exc).__name__,
            "CropFinding exceeded the configured overall operation timeout.",
        )
        timeout_seconds = settings.crop_finding_overall_timeout_seconds
        detail = CropFindingErrorDetail(
            code="CROP_FINDING_TIMEOUT",
            message=f"CropFinding exceeded the {timeout_seconds:g}-second operational deadline.",
            requestId=request_id,
            operation=operation_name,
            stage=stage,
            attempt=attempt,
            category="timeout",
            configuredTimeoutSeconds=timeout_seconds,
            effectiveTimeoutSeconds=timeout_seconds,
        )
        raise HTTPException(
            status_code=504,
            detail=detail.model_dump(mode="json", by_alias=True, exclude_none=True),
        ) from exc
    except LLMProviderError as exc:
        logger.warning(
            "CropFinding provider failure requestId=%s action=%s adminUserId=%s operation=%s stage=%s attempt=%s "
            "exceptionClass=%s rootCauseClass=%s category=%s safeMessage=%s httpStatus=%s "
            "openaiErrorCode=%s providerRequestId=%s configuredTimeoutSeconds=%s effectiveTimeoutSeconds=%s "
            "elapsedOperationMs=%s remainingBudgetSeconds=%s sourceCount=%s chunkCount=%s extractedCharacterCount=%s",
            exc.request_id or request_id,
            action,
            admin_user_id,
            exc.operation,
            exc.stage,
            exc.attempt,
            type(exc).__name__,
            exc.root_exception_class,
            exc.category,
            str(exc),
            exc.status_code,
            exc.error_code,
            exc.provider_request_id,
            exc.configured_timeout_seconds,
            exc.effective_timeout_seconds,
            exc.elapsed_operation_ms,
            exc.remaining_budget_seconds,
            exc.source_count,
            exc.chunk_count,
            exc.extracted_character_count,
        )
        is_timeout = exc.category == "timeout"
        detail = CropFindingErrorDetail(
            code="CROP_FINDING_TIMEOUT" if is_timeout else "CROP_FINDING_PROVIDER_FAILURE",
            message=str(exc),
            requestId=exc.request_id or request_id,
            operation=exc.operation,
            stage=exc.stage,
            attempt=exc.attempt,
            category=exc.category,
            upstreamStatus=exc.status_code,
            providerErrorCode=exc.error_code,
            providerRequestId=exc.provider_request_id,
            configuredTimeoutSeconds=exc.configured_timeout_seconds,
            effectiveTimeoutSeconds=exc.effective_timeout_seconds,
        )
        raise HTTPException(
            status_code=504 if is_timeout else 502,
            detail=detail.model_dump(mode="json", by_alias=True, exclude_none=True),
        ) from exc

    duration_ms = round((time.monotonic() - started) * 1000)
    payload = result.model_dump(mode="json", by_alias=True) if hasattr(result, "model_dump") else {}
    sources = payload.get("sources") or [draft.get("source", {}) for draft in payload.get("sourceDrafts", [])]
    items = payload.get("suggestions") or [
        item for draft in payload.get("sourceDrafts", []) for item in draft.get("items", [])
    ]
    counts: dict[str, int] = {}
    for item in items:
        status = str(item.get("evidenceStatus") or "Unknown")
        counts[status] = counts.get(status, 0) + 1
    logger.info(
        "CropFinding request completed requestId=%s action=%s adminUserId=%s durationMs=%s usedInternationalFallback=%s sourceUrls=%s evidenceCounts=%s outcome=success",
        request_id,
        action,
        admin_user_id,
        duration_ms,
        payload.get("usedInternationalFallback", False),
        [source.get("finalUrl") or source.get("originalUrl") for source in sources],
        counts,
    )
    return result


@app.post(
    "/crop-finding/suggest-crops",
    response_model=CropSuggestionsResponse,
    dependencies=[Depends(require_service_token)],
)
async def suggest_crops(
    request: SuggestCropsInput,
    settings: Settings = Depends(get_settings),
) -> CropSuggestionsResponse:
    return await _run_crop_finding(
        action="SuggestCrops",
        admin_user_id=str(request.admin_user_id),
        crop=None,
        variety=None,
        settings=settings,
        operation=lambda agent: agent.suggest_crops(request),
    )


@app.post(
    "/crop-finding/suggest-varieties",
    response_model=VarietySuggestionsResponse,
    dependencies=[Depends(require_service_token)],
)
async def suggest_varieties(
    request: SuggestVarietiesInput,
    settings: Settings = Depends(get_settings),
) -> VarietySuggestionsResponse:
    return await _run_crop_finding(
        action="SuggestVarieties",
        admin_user_id=str(request.admin_user_id),
        crop=request.crop_name,
        variety=None,
        settings=settings,
        operation=lambda agent: agent.suggest_varieties(request),
    )


@app.post(
    "/crop-finding/discover-references",
    response_model=ReferenceDiscoveryResponse,
    dependencies=[Depends(require_service_token)],
)
async def discover_references(
    request: DiscoverReferencesInput,
    settings: Settings = Depends(get_settings),
) -> ReferenceDiscoveryResponse:
    return await _run_crop_finding(
        action="DiscoverReferences",
        admin_user_id=str(request.admin_user_id),
        crop=request.crop_name,
        variety=request.variety_name,
        settings=settings,
        operation=lambda agent: agent.discover_references(request),
    )


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


@app.post(
    "/workflows/crop-planning/inspection-note-assistance",
    response_model=InspectionNoteAssistanceOutput,
    dependencies=[Depends(require_service_token)],
)
async def run_inspection_note_assistance(
    request: InspectionNoteAssistanceInput,
    settings: Settings = Depends(get_settings),
) -> InspectionNoteAssistanceOutput:
    return await InspectionNoteAssistantAgent(
        provider=create_provider(settings),
        timeout_seconds=min(settings.provider_timeout_seconds, 30),
    ).run(request)


@app.get(
    "/workflows/crop-planning/inspection-image-analysis/capability",
    response_model=InspectionImageAnalysisCapabilityResponse,
    dependencies=[Depends(require_service_token)],
)
async def get_inspection_image_analysis_capability(
    settings: Settings = Depends(get_settings),
) -> InspectionImageAnalysisCapabilityResponse:
    policy = SourcePolicy.load_default()
    return InspectionImageAnalysisCapabilityResponse(
        contractVersion=INSPECTION_IMAGE_ANALYSIS_CONTRACT_VERSION,
        imagePreprocessingVersion=IMAGE_PREPROCESSING_VERSION,
        promptContractVersion=INSPECTION_IMAGE_PROMPT_CONTRACT_VERSION,
        relevanceRuleVersion=RELEVANCE_RULE_VERSION,
        sourcePolicyVersion=policy.version,
        sourcePolicyHash=policy.content_hash,
        provider=settings.ai_provider,
        model=settings.ai_model or "unavailable",
    )


@app.post(
    "/workflows/crop-planning/inspection-image-analysis",
    response_model=InspectionImageAnalysisOperationResponse,
    dependencies=[Depends(require_service_token)],
)
async def run_inspection_image_analysis(
    request: InspectionImageAnalysisInput,
    settings: Settings = Depends(get_settings),
) -> InspectionImageAnalysisOperationResponse:
    source_policy = SourcePolicy.load_default()
    agent = InspectionImageAnalysisAgent(
        tools=InspectionImageAnalysisTools(BackendToolClient(settings)),
        provider=create_provider(settings),
        evidence_adapter=CropHealthEvidenceAdapter(
            settings,
            tools=CropFindingTools(settings, source_policy=source_policy),
            source_policy=source_policy,
        ),
        pass1_timeout_seconds=settings.inspection_image_pass1_timeout_seconds,
        pass2_timeout_seconds=settings.inspection_image_pass2_timeout_seconds,
    )
    try:
        return await asyncio.wait_for(agent.run(request), timeout=settings.inspection_image_overall_timeout_seconds)
    except asyncio.TimeoutError:
        return agent._failure(
            "operation_timeout",
            "Image analysis exceeded its bounded operation time.",
            status="TimedOut",
        )

@app.post(
    "/workflows/crop-planning/weather-resource",
    response_model=WeatherResourceOutput,
    dependencies=[Depends(require_service_token)],
)
async def run_weather_resource_analysis(
    request: WeatherResourceInput,
    settings: Settings = Depends(get_settings),
) -> WeatherResourceOutput:
    # The agent gathers verified requirements, field, inventory, reservations and weather through read-only backend tools
    # and calculates every figure with fixed rules. OpenAI, when configured, only writes the weather-risk explanation;
    # without OPENAI_API_KEY the provider is None and a rule-based explanation is returned instead.
    agent = WeatherResourceAgent(
        tools=WeatherResourceTools(BackendToolClient(settings)),
        llm_provider=create_provider(settings),
        explanation_timeout_seconds=settings.provider_timeout_seconds,
    )
    graph = build_weather_resource_graph(agent)
    state = await graph.ainvoke({"request": request, "output": None})
    return state["output"]


@app.post(
    "/workflows/crop-planning/scheduling-validation",
    response_model=SchedulingValidationOutput,
    dependencies=[Depends(require_service_token)],
)
async def run_scheduling_validation(
    request: SchedulingValidationInput,
    settings: Settings = Depends(get_settings),
) -> SchedulingValidationOutput:
    profile_retriever = None
    if settings.scheduling_profile_retrieval_enabled:
        provider = create_provider(settings)
        if provider is not None and settings.backend_tool_token:
            profile_retriever = SchedulingProfileRetriever(
                provider=provider,
                tools=SchedulingEvidenceTools(CropPlanningTools(BackendToolClient(settings))),
            )
    agent = SchedulingValidationAgent(profile_retriever=profile_retriever)
    graph = build_scheduling_validation_graph(agent)
    state = await graph.ainvoke({"request": request, "output": None})
    return state["output"]
