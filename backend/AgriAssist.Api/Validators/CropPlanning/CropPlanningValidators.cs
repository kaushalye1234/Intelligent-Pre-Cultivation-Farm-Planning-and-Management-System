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

public sealed class CropVarietyRequestValidator : IRequestValidator<CropVarietyRequest>
{
    public IReadOnlyList<string> Validate(CropVarietyRequest request)
    {
        var errors = new List<string>();
        if (request.CropTypeId == Guid.Empty) errors.Add("Crop type is required.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120) errors.Add("Variety name is required and must be 120 characters or fewer.");
        return errors;
    }
}

public sealed class CropReferenceProfileRequestValidator : IRequestValidator<CropReferenceProfileRequest>
{
    public IReadOnlyList<string> Validate(CropReferenceProfileRequest request)
    {
        var errors = new List<string>();
        if (request.CropTypeId == Guid.Empty) errors.Add("Crop type is required.");
        if (request.Region?.Length > 120) errors.Add("Region must be 120 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.SourceName) || request.SourceName.Length > 180) errors.Add("Source name is required and must be 180 characters or fewer.");
        if (request.SourceUrl?.Length > 1000) errors.Add("Source URL must be 1000 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.SourceVersion) || request.SourceVersion.Length > 120) errors.Add("Source version is required and must be 120 characters or fewer.");
        if (request.VerifiedAt == default || request.VerifiedAt > DateTime.UtcNow) errors.Add("Verification date must be in the past.");
        if (request.Stages is null || request.Rules is null || request.Stages.Count + request.Rules.Count == 0) errors.Add("At least one reference stage or rule is required.");
        foreach (var stage in request.Stages ?? [])
        {
            if (string.IsNullOrWhiteSpace(stage.StageName) || stage.StageName.Length > 120) errors.Add("Stage name is required and must be 120 characters or fewer.");
            if (stage.Sequence < 1) errors.Add("Stage sequence must be positive.");
            if (stage.TypicalMinDays < 0 || stage.TypicalMaxDays < 0 ||
                (stage.TypicalMinDays.HasValue && stage.TypicalMaxDays.HasValue && stage.TypicalMaxDays < stage.TypicalMinDays)) errors.Add("Stage duration range is invalid.");
            if (stage.Notes?.Length > 1000) errors.Add("Stage notes must be 1000 characters or fewer.");
        }
        foreach (var rule in request.Rules ?? [])
        {
            if (string.IsNullOrWhiteSpace(rule.RuleType) || rule.RuleType.Length > 120) errors.Add("Rule type is required and must be 120 characters or fewer.");
            if (string.IsNullOrWhiteSpace(rule.RuleKey) || rule.RuleKey.Length > 160) errors.Add("Rule key is required and must be 160 characters or fewer.");
            if (string.IsNullOrWhiteSpace(rule.StructuredValueJson))
            {
                errors.Add("Rule value must be valid JSON.");
                continue;
            }
            try { using var _ = System.Text.Json.JsonDocument.Parse(rule.StructuredValueJson); }
            catch (System.Text.Json.JsonException) { errors.Add("Rule value must be valid JSON."); }
        }
        return errors;
    }
}

public sealed class CropPlanRequestCreateValidator : IRequestValidator<CropPlanRequestCreate>
{
    private static readonly HashSet<string> KnownProblemCodes = new(StringComparer.Ordinal)
    {
        "PreviousFlooding", "PreviousWaterShortage", "PreviousPestIssue",
        "PreviousDiseaseIssue", "PreviousSoilProblem", "Other", "NoneKnown"
    };

    public IReadOnlyList<string> Validate(CropPlanRequestCreate request)
    {
        var errors = new List<string>();
        if (request.FarmId == Guid.Empty) errors.Add("Farm is required.");
        if (!request.FieldId.HasValue || request.FieldId == Guid.Empty) errors.Add("Field is required.");
        if (request.CropTypeId == Guid.Empty) errors.Add("Crop type is required.");
        if (request.CropVarietyId == Guid.Empty) errors.Add("Crop variety is invalid.");
        if (request.PreviousCropTypeId == Guid.Empty) errors.Add("Previous crop is invalid.");
        if (!Enum.IsDefined(request.CultivationSeason)) errors.Add("Cultivation season is invalid.");
        var problems = request.PreviousKnownProblems ?? [];
        if (problems.Count > 7 || problems.Any(code => !KnownProblemCodes.Contains(code)) || problems.Distinct(StringComparer.Ordinal).Count() != problems.Count)
            errors.Add("Previous known problems contain invalid or duplicate values.");
        if (problems.Contains("NoneKnown") && problems.Count > 1) errors.Add("None known cannot be combined with other previous problems.");
        if (request.PreferredStartDate == default) errors.Add("Preferred start date is required.");
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

public sealed class PrePlantingAssessmentRequestValidator : IRequestValidator<PrePlantingAssessmentRequest>
{
    public IReadOnlyList<string> Validate(PrePlantingAssessmentRequest request)
    {
        var errors = new List<string>();
        ValidateRequired(request.SoilCondition, 240, "Soil type / condition", errors);
        ValidateRequired(request.WaterAvailability, 500, "Water availability", errors);
        ValidateRequired(request.IrrigationAvailability, 500, "Irrigation availability", errors);
        ValidateRequired(request.DrainageCondition, 500, "Drainage condition", errors);
        ValidateRequired(request.GeneralFieldCondition, 1000, "General field condition", errors);
        ValidateRequired(request.PlantingReadiness, 500, "Planting readiness", errors);
        ValidateRequired(request.RisksAndConcerns, 1500, "Risks / concerns", errors);
        ValidateRequired(request.OfficerNotes, 2000, "Officer notes", errors);
        return errors;
    }

    private static void ValidateRequired(string value, int maxLength, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            errors.Add($"{name} is required and must be {maxLength} characters or fewer.");
        }
    }
}
