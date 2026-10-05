using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Validators.Shared;

namespace AgriAssist.Api.Validators.Resources;

public sealed class ResourceRequirementResearchRequestValidator : IRequestValidator<ResourceRequirementResearchRequest>
{
    public IReadOnlyList<string> Validate(ResourceRequirementResearchRequest request)
    {
        var errors = new List<string>();
        if (request.CropTypeId == Guid.Empty) errors.Add("Crop type is required.");
        if (request.ResourceId == Guid.Empty) errors.Add("Resource is required.");
        if (request.CropVarietyId == Guid.Empty) errors.Add("Crop variety is invalid.");
        if (request.Region?.Length > 120) errors.Add("Region must be 120 characters or fewer.");
        return errors;
    }
}

public sealed class VerifyResourceRequirementRequestValidator : IRequestValidator<VerifyResourceRequirementRequest>
{
    /// <summary>Upper bound that catches unit or decimal-point mistakes; no crop needs this much per acre or hectare.</summary>
    public const decimal MaxQuantityPerArea = 100_000m;

    public IReadOnlyList<string> Validate(VerifyResourceRequirementRequest request)
    {
        var errors = new List<string>();
        if (request.CropTypeId == Guid.Empty) errors.Add("Crop type is required.");
        if (request.ResourceId == Guid.Empty) errors.Add("Resource is required.");
        if (request.CropVarietyId == Guid.Empty) errors.Add("Crop variety is invalid.");
        if (request.Region?.Length > 120) errors.Add("Region must be 120 characters or fewer.");
        if (request.QuantityPerArea <= 0 || request.QuantityPerArea > MaxQuantityPerArea)
            errors.Add("Quantity per area must be positive and realistic.");
        if (decimal.Round(request.QuantityPerArea, 3) != request.QuantityPerArea)
            errors.Add("Quantity per area can have at most three decimal places.");
        if (string.IsNullOrWhiteSpace(request.ResourceUnit) || request.ResourceUnit.Length > 40) errors.Add("Resource unit is required.");
        if (CropResourceRequirementRule.NormalizeAreaUnit(request.AreaUnit) is null) errors.Add("Area unit must be acre or hectare.");
        if (string.IsNullOrWhiteSpace(request.SourceName) || request.SourceName.Length > 180)
            errors.Add("Source name is required and must be 180 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.SourceUrl) || request.SourceUrl.Length > 1000
            || !Uri.TryCreate(request.SourceUrl.Trim(), UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
            errors.Add("Source URL must be an absolute http or https address.");
        if (string.IsNullOrWhiteSpace(request.Evidence) || request.Evidence.Length > 1200)
            errors.Add("Evidence is required and must be 1200 characters or fewer.");
        if (request.ResearchRequestId?.Length > 200) errors.Add("Research request id is too long.");
        return errors;
    }
}
