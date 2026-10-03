import asyncio
import json
from typing import Protocol

from pydantic import ValidationError

from agents.inspection_image_preprocessor import ImagePreprocessingError, InspectionImagePreprocessor
from providers.base_llm_provider import BaseLLMProvider, LLMProviderError, StructuredGenerationRequest
from schemas.inspection_image_analysis import (
    INSPECTION_IMAGE_ANALYSIS_CONTRACT_VERSION,
    INSPECTION_IMAGE_PASS1_CONTRACT_VERSION,
    GroundingEvidence,
    InspectionImageAnalysisInput,
    InspectionImageAnalysisOperationResponse,
    InspectionImageAnalysisResult,
    InspectionImagePass1Result,
)
from tools.backend_tool_client import ToolClientError
from tools.inspection_image_analysis_tools import InspectionImageAnalysisTools


class CropHealthEvidenceAdapter(Protocol):
    async def retrieve(
        self,
        request: InspectionImageAnalysisInput,
        pass1: InspectionImagePass1Result,
    ) -> list[GroundingEvidence]: ...


class NoEvidenceAdapter:
    async def retrieve(
        self,
        request: InspectionImageAnalysisInput,
        pass1: InspectionImagePass1Result,
    ) -> list[GroundingEvidence]:
        return []


class InspectionImageAnalysisAgent:
    """Acyclic retrieve -> preprocess -> Pass 1 -> evidence -> Pass 2 pipeline."""

    def __init__(
        self,
        tools: InspectionImageAnalysisTools,
        provider: BaseLLMProvider | None,
        evidence_adapter: CropHealthEvidenceAdapter,
        pass1_timeout_seconds: float = 30,
        pass2_timeout_seconds: float = 45,
    ) -> None:
        self._tools = tools
        self._provider = provider
        self._evidence_adapter = evidence_adapter
        self._pass1_timeout_seconds = pass1_timeout_seconds
        self._pass2_timeout_seconds = pass2_timeout_seconds

    async def run(self, request: InspectionImageAnalysisInput) -> InspectionImageAnalysisOperationResponse:
        if self._provider is None:
            return self._failure("provider_unavailable", "Image analysis is currently unavailable.")
        try:
            original = await self._tools.get_verified_image(request.analysis_id)
            normalized = InspectionImagePreprocessor.preprocess(
                original.image_bytes,
                original.content_type,
                request.image_preprocessing_version,
            )
        except ToolClientError:
            return self._failure("image_retrieval_failed", "The verified inspection image could not be retrieved.")
        except ImagePreprocessingError as exc:
            return self._failure(str(exc), "The inspection image could not be prepared safely for analysis.")

        try:
            pass1 = await self._vision_pass1(request, normalized.image_bytes, normalized.mime_type)
        except LLMProviderError as exc:
            return self._provider_failure(exc)
        except ValidationError:
            return self._failure("pass1_schema_mismatch", "The visual analysis result did not match its contract.")

        try:
            evidence = await self._evidence_adapter.retrieve(request, pass1)
        except Exception:
            evidence = []

        try:
            final = await self._grounded_pass2(request, pass1, evidence)
            self._validate_final(final, evidence)
            return InspectionImageAnalysisOperationResponse(
                contractVersion=INSPECTION_IMAGE_ANALYSIS_CONTRACT_VERSION,
                status="Succeeded",
                pass1Result=pass1,
                evidencePacket=evidence,
                finalResult=final,
                failureCategory=None,
                failureMessage=None,
            )
        except LLMProviderError as exc:
            return self._provider_failure(exc, pass1, evidence)
        except (ValidationError, ValueError):
            return self._failure(
                "pass2_schema_mismatch",
                "The grounded analysis result did not match its contract.",
                pass1,
                evidence,
            )

    async def _vision_pass1(
        self,
        request: InspectionImageAnalysisInput,
        image_bytes: bytes,
        mime_type: str,
    ) -> InspectionImagePass1Result:
        context = json.dumps({"cropName": request.crop_name, "varietyName": request.variety_name}, separators=(",", ":"))
        generated = await self._provider.generate_structured_json(  # type: ignore[union-attr]
            StructuredGenerationRequest(
                schema_name="inspection_image_pass1_v1",
                contract_version=INSPECTION_IMAGE_PASS1_CONTRACT_VERSION,
                response_schema=InspectionImagePass1Result.model_json_schema(by_alias=True),
                system_input=(
                    "Describe only visible crop/leaf findings. Suggest broad possible issue categories with uncertainty; "
                    "one image is not a diagnosis. Do not name chemicals or treatments. Return no more than three short, "
                    "category-based search intents with exactly one primary intent when any intent is returned."
                ),
                user_input=context,
                timeout_seconds=self._pass1_timeout_seconds,
                image_bytes=image_bytes,
                image_mime_type=mime_type,
            )
        )
        return InspectionImagePass1Result.model_validate_json(generated.text)

    async def _grounded_pass2(
        self,
        request: InspectionImageAnalysisInput,
        pass1: InspectionImagePass1Result,
        evidence: list[GroundingEvidence],
    ) -> InspectionImageAnalysisResult:
        packet = {
            "cropName": request.crop_name,
            "varietyName": request.variety_name,
            "visual": pass1.model_dump(mode="json", by_alias=True),
            "evidence": [item.model_dump(mode="json", by_alias=True) for item in evidence],
        }
        generated = await self._provider.generate_structured_json(  # type: ignore[union-attr]
            StructuredGenerationRequest(
                schema_name="inspection_image_analysis_v1",
                contract_version=INSPECTION_IMAGE_ANALYSIS_CONTRACT_VERSION,
                response_schema=InspectionImageAnalysisResult.model_json_schema(by_alias=True),
                system_input=(
                    "Synthesize the visual findings using only supplied context and validated evidence. Preserve uncertainty; "
                    "never claim a confirmed diagnosis from one image. Recommendations must use only allowed non-chemical "
                    "action identifiers. If evidence is empty, use GroundingStatus Unavailable, cite no sources, remain "
                    "conservative, and require further assessment."
                ),
                user_input=json.dumps(packet, separators=(",", ":"), ensure_ascii=False),
                timeout_seconds=self._pass2_timeout_seconds,
            )
        )
        return InspectionImageAnalysisResult.model_validate_json(generated.text)

    @staticmethod
    def _validate_final(final: InspectionImageAnalysisResult, evidence: list[GroundingEvidence]) -> None:
        certain_phrases = ("confirmed", "definitive diagnosis", "proven disease", "proven pest")
        text = " ".join([*final.visible_findings, *final.possible_issues, final.uncertainty]).casefold()
        if any(phrase in text for phrase in certain_phrases):
            raise ValueError("Diagnosis certainty is not permitted.")
        evidence_ids = {item.source_policy_id for item in evidence}
        if any(source.source_policy_id not in evidence_ids for source in final.validated_source_references):
            raise ValueError("Final source references must come from the supplied evidence packet.")
        if not evidence and (
            final.grounding_status != "Unavailable"
            or final.validated_source_references
            or not final.requires_further_assessment
        ):
            raise ValueError("Ungrounded output must remain conservative.")
        if evidence and final.grounding_status != "Grounded":
            raise ValueError("Validated evidence must be represented as grounded.")

    @staticmethod
    def _failure(
        category: str,
        message: str,
        pass1: InspectionImagePass1Result | None = None,
        evidence: list[GroundingEvidence] | None = None,
        status: str = "Failed",
    ) -> InspectionImageAnalysisOperationResponse:
        return InspectionImageAnalysisOperationResponse(
            contractVersion=INSPECTION_IMAGE_ANALYSIS_CONTRACT_VERSION,
            status=status,
            pass1Result=pass1,
            evidencePacket=evidence or [],
            finalResult=None,
            failureCategory=category[:100],
            failureMessage=message[:300],
        )

    @classmethod
    def _provider_failure(
        cls,
        error: LLMProviderError,
        pass1: InspectionImagePass1Result | None = None,
        evidence: list[GroundingEvidence] | None = None,
    ) -> InspectionImageAnalysisOperationResponse:
        status = "TimedOut" if error.category == "timeout" else "Failed"
        return cls._failure(error.category, "The image-analysis provider could not complete the request.", pass1, evidence, status)
