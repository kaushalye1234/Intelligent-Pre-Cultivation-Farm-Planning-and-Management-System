from agents.evidence_scheduler import EvidenceScheduler
from schemas.scheduling_validation import SchedulingValidationInput, SchedulingValidationOutput


class SchedulingValidationAgent:
    """Builds deterministic, proposal-only schedules from persisted evidence."""

    async def run(self, request: SchedulingValidationInput) -> SchedulingValidationOutput:
        return EvidenceScheduler.run(request)

    def check_dependencies(self, request: SchedulingValidationInput) -> SchedulingValidationOutput | None:
        return EvidenceScheduler.check_dependencies(request)

    def propose(self, request: SchedulingValidationInput) -> SchedulingValidationOutput:
        return EvidenceScheduler.propose(request)

    def assess_risk(self, request: SchedulingValidationInput,
                    draft: SchedulingValidationOutput) -> SchedulingValidationOutput:
        return EvidenceScheduler.assess_risk(request, draft)
