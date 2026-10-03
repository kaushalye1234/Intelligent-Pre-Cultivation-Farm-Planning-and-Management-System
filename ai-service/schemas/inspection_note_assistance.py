from typing import Literal

from pydantic import ConfigDict, Field

from schemas.common import CamelModel, to_camel

INSPECTION_NOTE_ASSISTANCE_CONTRACT_VERSION = 1


class StrictCamelModel(CamelModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="forbid")


class InspectionNoteDraft(StrictCamelModel):
    soil_type: str | None = Field(default=None, max_length=80)
    soil_condition: str | None = Field(default=None, max_length=80)
    soil_moisture: str | None = Field(default=None, max_length=80)
    water_availability: str | None = Field(default=None, max_length=80)
    main_water_source: str | None = Field(default=None, max_length=160)
    irrigation_availability: str | None = Field(default=None, max_length=80)
    water_reliability: str | None = Field(default=None, max_length=80)
    drainage_condition: str | None = Field(default=None, max_length=80)
    waterlogging_risk: str | None = Field(default=None, max_length=80)
    general_field_condition: str | None = Field(default=None, max_length=80)
    planting_readiness: str | None = Field(default=None, max_length=80)
    identified_risks: list[str] = Field(default_factory=list, max_length=12)
    soil_notes: str | None = Field(default=None, max_length=1000)
    water_concerns: str | None = Field(default=None, max_length=1000)
    drainage_notes: str | None = Field(default=None, max_length=1000)
    general_field_notes: str | None = Field(default=None, max_length=1000)
    risk_notes: str | None = Field(default=None, max_length=1000)
    officer_notes: str | None = Field(default=None, max_length=1000)


class InspectionNoteAssistanceInput(StrictCamelModel):
    contract_version: Literal[1]
    crop_name: str = Field(min_length=1, max_length=120)
    variety_name: str | None = Field(default=None, max_length=120)
    field_name: str = Field(min_length=1, max_length=160)
    field_soil_type: str | None = Field(default=None, max_length=120)
    draft: InspectionNoteDraft


class InspectionNoteSuggestions(StrictCamelModel):
    soil_notes: str | None = Field(max_length=800)
    water_concerns: str | None = Field(max_length=800)
    drainage_notes: str | None = Field(max_length=800)
    general_field_notes: str | None = Field(max_length=800)
    risk_notes: str | None = Field(max_length=800)
    officer_notes: str | None = Field(max_length=800)


class InspectionNoteAssistanceOutput(StrictCamelModel):
    contract_version: Literal[1]
    status: Literal["Available", "Unavailable"]
    suggestions: InspectionNoteSuggestions | None
    contradiction_warnings: list[str] = Field(max_length=8)
    missing_data_warnings: list[str] = Field(max_length=8)
    failure_category: str | None = Field(max_length=80)
