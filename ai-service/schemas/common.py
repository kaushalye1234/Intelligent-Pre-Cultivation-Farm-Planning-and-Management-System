from pydantic import BaseModel, ConfigDict, Field


def to_camel(value: str) -> str:
    first, *rest = value.split("_")
    return first + "".join(word.capitalize() for word in rest)


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class AgentEnvelope(CamelModel):
    workflow_id: str = Field(alias="workflowId")
    status: str
    requires_human_review: bool = Field(alias="requiresHumanReview")
    warnings: list[str] = Field(default_factory=list)
