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
}
