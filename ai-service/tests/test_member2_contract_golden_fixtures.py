import json
from pathlib import Path
from typing import get_args

import pytest
from pydantic import ValidationError

from schemas.field_analysis import CropFieldAnalysisOutput, ReviewedImageAnalysisInputProjection
from schemas.inspection_image_analysis import (
    CropHealthActionType,
    InspectionImageAnalysisResult,
    InspectionImagePass1Result,
)
from schemas.inspection_note_assistance import InspectionNoteAssistanceOutput
from schemas.weather_resource import CropHealthWeatherResourceConsideration


FIXTURES = Path(__file__).resolve().parents[2] / "docs" / "ai-usage" / "fixtures" / "member2"
EXPECTED_ACTIONS = {
    "FieldSanitation",
    "RemoveAffectedResidue",
    "SeparateAffectedMaterial",
    "InspectNearbyPlants",
    "MonitorSymptoms",
    "PrePlantingCleanup",
    "RequestFurtherAssessment",
}


def fixture(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text(encoding="utf-8"))


def test_native_member2_golden_contracts_parse_and_serialize() -> None:
    note = InspectionNoteAssistanceOutput.model_validate(fixture("note-assistance.valid.json"))
    pass1 = InspectionImagePass1Result.model_validate(fixture("image-pass1.valid.json"))
    final = InspectionImageAnalysisResult.model_validate(fixture("image-analysis.valid.json"))
    reviewed = ReviewedImageAnalysisInputProjection.model_validate(fixture("reviewed-projection.edited.valid.json"))
    consideration = CropHealthWeatherResourceConsideration.model_validate(fixture("member3-consideration.valid.json"))

    assert note.model_dump(by_alias=True)["status"] == "Available"
    assert pass1.model_dump(by_alias=True)["contractVersion"] == 1
    assert final.model_dump(by_alias=True)["recommendedNonChemicalActions"][0] == "RemoveAffectedResidue"
    assert reviewed.model_dump(by_alias=True)["contractVersion"] == 1
    assert consideration.model_dump(by_alias=True)["contractVersion"] == 1


@pytest.mark.parametrize(
    ("model", "name"),
    [
        (InspectionNoteAssistanceOutput, "note-assistance.invalid-unknown-field.json"),
        (InspectionImagePass1Result, "image-pass1.invalid-category.json"),
        (InspectionImageAnalysisResult, "image-analysis.invalid-action.json"),
    ],
)
def test_invalid_golden_contracts_are_rejected(model: type, name: str) -> None:
    with pytest.raises(ValidationError):
        model.model_validate(fixture(name))


def test_field_analysis_model_cannot_author_backend_carried_actions() -> None:
    persisted = fixture("field-analysis.valid.json")
    with pytest.raises(ValidationError):
        CropFieldAnalysisOutput.model_validate(persisted)

    model_owned = dict(persisted)
    model_owned.pop("reviewedCropIssueActions")
    model_owned.pop("reviewedCropHealthGuidance")
    parsed = CropFieldAnalysisOutput.model_validate(model_owned)
    assert parsed.contract_version == 2


def test_python_action_identifier_mirror_matches_backend_fixture_catalog() -> None:
    assert set(get_args(CropHealthActionType)) == EXPECTED_ACTIONS
