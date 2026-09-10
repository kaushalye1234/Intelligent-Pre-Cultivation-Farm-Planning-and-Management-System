using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Validators.CropPlanning;
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
    public void Reservation_validator_rejects_zero_quantity()
    {
        var validator = new ResourceReservationRequestValidator();
        var request = new ResourceReservationRequest(Guid.NewGuid(), 0, "Need seed");

        var errors = validator.Validate(request);

        Assert.Contains(errors, error => error.Contains("positive"));
    }
}
