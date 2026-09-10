using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Validators.Shared;

namespace AgriAssist.Api.Validators.Inspections;

public sealed class FieldInspectionRequestValidator : IRequestValidator<FieldInspectionRequest>
{
    public IReadOnlyList<string> Validate(FieldInspectionRequest request)
    {
        var errors = new List<string>();
        if (request.FieldId == Guid.Empty) errors.Add("Field is required.");
        if (request.Summary.Length > 1000) errors.Add("Summary must be 1000 characters or fewer.");
        return errors;
    }
}

public sealed class ObservationRequestValidator : IRequestValidator<ObservationRequest>
{
    public IReadOnlyList<string> Validate(ObservationRequest request)
    {
        var errors = new List<string>();
        if (request.FieldInspectionId == Guid.Empty) errors.Add("Inspection is required.");
        if (string.IsNullOrWhiteSpace(request.ObservationType) || request.ObservationType.Length > 120) errors.Add("Observation type is required and must be 120 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Length > 1000) errors.Add("Observation notes are required and must be 1000 characters or fewer.");
        return errors;
    }
}

public sealed class CropIssueRequestValidator : IRequestValidator<CropIssueRequest>
{
    public IReadOnlyList<string> Validate(CropIssueRequest request)
    {
        var errors = new List<string>();
        if (request.FieldInspectionId == Guid.Empty) errors.Add("Inspection is required.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 160) errors.Add("Issue title is required and must be 160 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 1500) errors.Add("Issue description is required and must be 1500 characters or fewer.");
        return errors;
    }
}

public sealed class FollowUpRecommendationRequestValidator : IRequestValidator<FollowUpRecommendationRequest>
{
    public IReadOnlyList<string> Validate(FollowUpRecommendationRequest request)
    {
        var errors = new List<string>();
        if (request.CropIssueId == Guid.Empty) errors.Add("Crop issue is required.");
        if (string.IsNullOrWhiteSpace(request.Recommendation) || request.Recommendation.Length > 1500) errors.Add("Recommendation is required and must be 1500 characters or fewer.");
        return errors;
    }
}
