using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Validators.CropPlanning;
using AgriAssist.Api.Validators.Inspections;
using AgriAssist.Api.Validators.Resources;

namespace AgriAssist.Api.Tests;

public sealed class ValidatorTests
{
    [Fact]
    public void Crop_plan_request_validator_rejects_invalid_dates_and_budget()
    {
        var validator = new CropPlanRequestCreateValidator();
        var request = new CropPlanRequestCreate(Guid.NewGuid(), null, Guid.NewGuid(), new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 1), 0, "");

        var errors = validator.Validate(request);

        Assert.Contains(errors, error => error.Contains("Preferred end date"));
        Assert.Contains(errors, error => error.Contains("Budget"));
        Assert.Contains(errors, error => error.Contains("Objective"));
    }

    [Fact]
    public void Crop_reference_validator_rejects_missing_rule_json_without_throwing()
    {
        var request = new CropReferenceProfileRequest(
            Guid.NewGuid(), null, null, "Verified source", null, "1",
            DateTime.UtcNow.AddDays(-1), [],
            [new CropReferenceRuleRequest("Season", "Maha", null!)]);

        var errors = new CropReferenceProfileRequestValidator().Validate(request);

        Assert.Contains(errors, error => error.Contains("Rule value must be valid JSON."));
    }

    [Fact]
    public void Reservation_validator_rejects_zero_quantity()
    {
        var validator = new ResourceReservationRequestValidator();
        var request = new ResourceReservationRequest(Guid.NewGuid(), 0, "Need seed");

        var errors = validator.Validate(request);

        Assert.Contains(errors, error => error.Contains("positive"));
    }

    [Fact]
    public void Inspection_validators_reject_invalid_enum_values()
    {
        var inspectionValidator = new FieldInspectionRequestValidator();
        var issueValidator = new CropIssueRequestValidator();

        var inspectionErrors = inspectionValidator.Validate(new FieldInspectionRequest(Guid.NewGuid(), DateTime.UtcNow, (InspectionStatus)99, "Summary"));
        var issueErrors = issueValidator.Validate(new CropIssueRequest(Guid.NewGuid(), "Issue", "Description", (CropIssueSeverity)99, (CropIssueStatus)99));

        Assert.Contains(inspectionErrors, error => error.Contains("status"));
        Assert.Contains(issueErrors, error => error.Contains("severity"));
        Assert.Contains(issueErrors, error => error.Contains("status"));
    }

    [Fact]
    public void Pre_planting_draft_validator_allows_an_empty_draft()
    {
        var errors = new PrePlantingAssessmentRequestValidator().Validate(new PrePlantingAssessmentRequest());

        Assert.Empty(errors);
    }

    [Fact]
    public void Pre_planting_draft_validator_rejects_only_invalid_supplied_values()
    {
        var request = new PrePlantingAssessmentRequest
        {
            SoilType = (PrePlantingSoilType)99,
            MainWaterSource = new string('x', 241),
            IdentifiedRisks = [PrePlantingRisk.PoorDrainage, PrePlantingRisk.PoorDrainage]
        };

        var errors = new PrePlantingAssessmentRequestValidator().Validate(request);

        Assert.Contains(errors, error => error.Contains("Soil type"));
        Assert.Contains(errors, error => error.Contains("Main water source"));
        Assert.Contains(errors, error => error.Contains("duplicate"));
        Assert.DoesNotContain(errors, error => error.Contains("Planting readiness is required"));
    }

    [Fact]
    public void Pre_planting_submission_requires_structured_observations_and_assessed_risks()
    {
        var errors = PrePlantingAssessmentRules.ValidateSubmission(new PrePlantingAssessmentRequest());

        Assert.Contains(errors, error => error.Contains("Soil type is required"));
        Assert.Contains(errors, error => error.Contains("Soil condition is required"));
        Assert.Contains(errors, error => error.Contains("Soil moisture is required"));
        Assert.Contains(errors, error => error.Contains("Water availability is required"));
        Assert.Contains(errors, error => error.Contains("Irrigation availability is required"));
        Assert.Contains(errors, error => error.Contains("Water reliability is required"));
        Assert.Contains(errors, error => error.Contains("Drainage condition is required"));
        Assert.Contains(errors, error => error.Contains("Waterlogging risk is required"));
        Assert.Contains(errors, error => error.Contains("General field condition is required"));
        Assert.Contains(errors, error => error.Contains("Planting readiness is required"));
        Assert.Contains(errors, error => error.Contains("Identified risks must be assessed"));
    }

    [Theory]
    [InlineData(PrePlantingWaterAvailability.Adequate, true)]
    [InlineData(PrePlantingWaterAvailability.Limited, true)]
    [InlineData(PrePlantingWaterAvailability.Seasonal, true)]
    [InlineData(PrePlantingWaterAvailability.Unavailable, false)]
    [InlineData(PrePlantingWaterAvailability.Unknown, false)]
    public void Pre_planting_submission_requires_water_source_only_when_applicable(
        PrePlantingWaterAvailability availability,
        bool requiresSource)
    {
        var request = ValidPrePlantingSubmission() with
        {
            WaterAvailability = availability,
            MainWaterSource = null,
            WaterConcerns = availability is PrePlantingWaterAvailability.Limited
                or PrePlantingWaterAvailability.Seasonal
                or PrePlantingWaterAvailability.Unavailable
                ? "Supply requires review."
                : null
        };

        var errors = PrePlantingAssessmentRules.ValidateSubmission(request);

        Assert.Equal(requiresSource, errors.Any(error => error.Contains("Main water source")));
    }

    [Fact]
    public void Pre_planting_submission_requires_conditional_notes()
    {
        var request = ValidPrePlantingSubmission() with
        {
            SoilType = PrePlantingSoilType.Other,
            GeneralFieldCondition = PrePlantingGeneralFieldCondition.Other,
            DrainageCondition = PrePlantingDrainageCondition.Poor,
            WaterloggingRisk = PrePlantingWaterloggingRisk.High,
            IdentifiedRisks = [PrePlantingRisk.Other],
            SoilNotes = null,
            GeneralFieldNotes = null,
            DrainageNotes = null,
            RiskNotes = null
        };

        var errors = PrePlantingAssessmentRules.ValidateSubmission(request);

        Assert.Contains(errors, error => error.Contains("Soil notes"));
        Assert.Contains(errors, error => error.Contains("General field notes"));
        Assert.Contains(errors, error => error.Contains("Drainage notes"));
        Assert.Contains(errors, error => error.Contains("Risk notes"));
    }

    private static PrePlantingAssessmentRequest ValidPrePlantingSubmission() =>
        new()
        {
            SoilType = PrePlantingSoilType.Loamy,
            SoilCondition = PrePlantingSoilCondition.Good,
            SoilMoisture = PrePlantingSoilMoisture.Moist,
            WaterAvailability = PrePlantingWaterAvailability.Adequate,
            MainWaterSource = "Canal",
            IrrigationAvailability = PrePlantingIrrigationAvailability.Available,
            WaterReliability = PrePlantingWaterReliability.Reliable,
            DrainageCondition = PrePlantingDrainageCondition.Good,
            WaterloggingRisk = PrePlantingWaterloggingRisk.NoneObserved,
            GeneralFieldCondition = PrePlantingGeneralFieldCondition.ClearAndPrepared,
            PlantingReadiness = PrePlantingPlantingReadiness.Ready,
            IdentifiedRisks = []
        };
}
