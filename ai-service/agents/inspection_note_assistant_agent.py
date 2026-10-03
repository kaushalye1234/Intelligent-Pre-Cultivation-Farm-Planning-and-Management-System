import json

from pydantic import ValidationError

from providers.base_llm_provider import BaseLLMProvider, LLMProviderError, StructuredGenerationRequest
from schemas.inspection_note_assistance import (
    INSPECTION_NOTE_ASSISTANCE_CONTRACT_VERSION,
    InspectionNoteAssistanceInput,
    InspectionNoteAssistanceOutput,
)


class InspectionNoteAssistantAgent:
    """One-call, text-only drafting assistance for the current inspection form."""

    def __init__(self, provider: BaseLLMProvider | None, timeout_seconds: float = 30) -> None:
        self._provider = provider
        self._timeout_seconds = timeout_seconds

    async def run(self, request: InspectionNoteAssistanceInput) -> InspectionNoteAssistanceOutput:
        if self._provider is None:
            return self._unavailable("provider_unavailable")

        prompt_input = json.dumps(
            request.model_dump(mode="json", by_alias=True),
            separators=(",", ":"),
            ensure_ascii=False,
        )
        try:
            generated = await self._provider.generate_structured_json(
                StructuredGenerationRequest(
                    schema_name="inspection_note_assistance_v1",
                    contract_version=INSPECTION_NOTE_ASSISTANCE_CONTRACT_VERSION,
                    response_schema=InspectionNoteAssistanceOutput.model_json_schema(by_alias=True),
                    system_input=(
                        "Draft concise Field Officer notes only from supplied structured observations and context. "
                        "Never invent field facts, measurements, diagnoses, pests, diseases, history, or treatments. "
                        "Use only the six fixed note fields; leave unsupported suggestions null. Surface contradictions "
                        "and missing information as advisory warnings. Return status Available and contractVersion 1."
                    ),
                    user_input=prompt_input,
                    timeout_seconds=self._timeout_seconds,
                )
            )
            output = InspectionNoteAssistanceOutput.model_validate_json(generated.text)
            if output.status != "Available" or output.suggestions is None:
                return self._unavailable("invalid_provider_result")
            output.failure_category = None
            return output
        except ValidationError:
            return self._unavailable("schema_mismatch")
        except LLMProviderError as exc:
            return self._unavailable(exc.category)
        except (ValueError, TypeError, json.JSONDecodeError):
            return self._unavailable("malformed_structured_output")

    @staticmethod
    def _unavailable(category: str) -> InspectionNoteAssistanceOutput:
        return InspectionNoteAssistanceOutput(
            contractVersion=INSPECTION_NOTE_ASSISTANCE_CONTRACT_VERSION,
            status="Unavailable",
            suggestions=None,
            contradictionWarnings=[],
            missingDataWarnings=[],
            failureCategory=category[:80],
        )
