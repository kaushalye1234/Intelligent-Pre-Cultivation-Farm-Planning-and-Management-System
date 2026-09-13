from schemas.common import CamelModel


class WorkflowStep(CamelModel):
    sequence: int
    step_type: str
    assigned_agent: str
