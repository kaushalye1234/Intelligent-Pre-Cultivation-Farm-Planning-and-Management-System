from agents.evidence_scheduler import EvidenceScheduler
from schemas.scheduling_validation import SchedulingValidationInput, SchedulingValidationOutput
from agents.scheduling_profile_retriever import SchedulingProfileRetriever


class SchedulingValidationAgent:
    """Builds deterministic, proposal-only schedules from persisted evidence."""

    def __init__(self, profile_retriever: SchedulingProfileRetriever | None = None) -> None:
        self._profile_retriever = profile_retriever

    async def retrieve_missing_profile(self, request: SchedulingValidationInput) -> SchedulingValidationInput:
        if self._profile_retriever is None:
            return request
        return await self._profile_retriever.retrieve_profile(request)

    async def run(self, request: SchedulingValidationInput) -> SchedulingValidationOutput:
        return EvidenceScheduler.run(request)

    def check_dependencies(self, request: SchedulingValidationInput) -> SchedulingValidationOutput | None:
        return EvidenceScheduler.check_dependencies(request)

    def propose(self, request: SchedulingValidationInput) -> SchedulingValidationOutput:
        return EvidenceScheduler.propose(request)

    def assess_risk(self, request: SchedulingValidationInput,
                    draft: SchedulingValidationOutput) -> SchedulingValidationOutput:
        return EvidenceScheduler.assess_risk(request, draft)
