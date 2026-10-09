using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Validators.Shared;

namespace AgriAssist.Api.Validators.CropPlanning;

public sealed class SuggestCropsRequestValidator : IRequestValidator<SuggestCropsRequest>
{
    public IReadOnlyList<string> Validate(SuggestCropsRequest request)
    {
        var errors = new List<string>();
        if (request.Context?.Length > 300) errors.Add("Search context must be 300 characters or fewer.");
        if (request.MaxSuggestions is < 1 or > 10) errors.Add("Maximum suggestions must be between 1 and 10.");
        return errors;
    }
}

public sealed class SuggestVarietiesRequestValidator : IRequestValidator<SuggestVarietiesRequest>
{
    public IReadOnlyList<string> Validate(SuggestVarietiesRequest request)
    {
        var errors = new List<string>();
        if (request.CropTypeId == Guid.Empty) errors.Add("Crop type is required.");
        if (request.Context?.Length > 300) errors.Add("Search context must be 300 characters or fewer.");
        if (request.MaxSuggestions is < 1 or > 10) errors.Add("Maximum suggestions must be between 1 and 10.");
        return errors;
    }
}

public sealed class DiscoverReferencesRequestValidator : IRequestValidator<DiscoverReferencesRequest>
{
    public IReadOnlyList<string> Validate(DiscoverReferencesRequest request)
    {
        var errors = new List<string>();
        if (request.CropTypeId == Guid.Empty) errors.Add("Crop type is required.");
        if (request.CropVarietyId == Guid.Empty) errors.Add("Crop variety is invalid.");
        if (request.Region?.Length > 120) errors.Add("Region must be 120 characters or fewer.");
        return errors;
    }
}
