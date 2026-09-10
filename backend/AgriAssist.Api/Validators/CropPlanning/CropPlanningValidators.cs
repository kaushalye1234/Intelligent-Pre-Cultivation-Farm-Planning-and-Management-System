using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Validators.Shared;

namespace AgriAssist.Api.Validators.CropPlanning;

public sealed class FarmRequestValidator : IRequestValidator<FarmRequest>
{
    public IReadOnlyList<string> Validate(FarmRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120) errors.Add("Farm name is required and must be 120 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.Location) || request.Location.Length > 240) errors.Add("Farm location is required and must be 240 characters or fewer.");
        if (request.TotalArea <= 0) errors.Add("Farm total area must be positive.");
        return errors;
    }
}

public sealed class FieldRequestValidator : IRequestValidator<FieldRequest>
{
    public IReadOnlyList<string> Validate(FieldRequest request)
    {
        var errors = new List<string>();
        if (request.FarmId == Guid.Empty) errors.Add("Farm is required.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120) errors.Add("Field name is required and must be 120 characters or fewer.");
        if (request.Area <= 0) errors.Add("Field area must be positive.");
        if (request.SoilType.Length > 120) errors.Add("Soil type must be 120 characters or fewer.");
        return errors;
    }
}

public sealed class CropTypeRequestValidator : IRequestValidator<CropTypeRequest>
{
    public IReadOnlyList<string> Validate(CropTypeRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120) errors.Add("Crop type name is required and must be 120 characters or fewer.");
        if (request.Description?.Length > 500) errors.Add("Crop type description must be 500 characters or fewer.");
        return errors;
    }
}

public sealed class CropCycleRequestValidator : IRequestValidator<CropCycleRequest>
{
    public IReadOnlyList<string> Validate(CropCycleRequest request)
    {
        var errors = new List<string>();
        if (request.FieldId == Guid.Empty) errors.Add("Field is required.");
        if (request.CropTypeId == Guid.Empty) errors.Add("Crop type is required.");
        if (request.PlannedEndDate <= request.PlannedStartDate) errors.Add("Planned end date must be after planned start date.");
        return errors;
    }
}

public sealed class CropPlanRequestCreateValidator : IRequestValidator<CropPlanRequestCreate>
{
    public IReadOnlyList<string> Validate(CropPlanRequestCreate request)
    {
        var errors = new List<string>();
        if (request.FarmId == Guid.Empty) errors.Add("Farm is required.");
        if (request.CropTypeId == Guid.Empty) errors.Add("Crop type is required.");
        if (request.PreferredEndDate <= request.PreferredStartDate) errors.Add("Preferred end date must be after preferred start date.");
        if (request.Budget <= 0) errors.Add("Budget must be positive.");
        if (string.IsNullOrWhiteSpace(request.Objective) || request.Objective.Length > 500) errors.Add("Objective is required and must be 500 characters or fewer.");
        return errors;
    }
}

public sealed class CropPlanRequestUpdateValidator : IRequestValidator<CropPlanRequestUpdate>
{
    public IReadOnlyList<string> Validate(CropPlanRequestUpdate request)
    {
        var errors = new List<string>();
        if (request.PreferredEndDate <= request.PreferredStartDate) errors.Add("Preferred end date must be after preferred start date.");
        if (request.Budget <= 0) errors.Add("Budget must be positive.");
        if (string.IsNullOrWhiteSpace(request.Objective) || request.Objective.Length > 500) errors.Add("Objective is required and must be 500 characters or fewer.");
        return errors;
    }
}
