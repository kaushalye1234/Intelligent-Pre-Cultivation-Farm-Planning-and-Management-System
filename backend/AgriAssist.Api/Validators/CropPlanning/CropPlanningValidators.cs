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
        ValidateEnum(request.SoilType, "Soil type", errors);
        ValidateEnum(request.SoilCondition, "Soil condition", errors);
        ValidateEnum(request.SoilMoisture, "Soil moisture", errors);
        ValidateEnum(request.WaterAvailability, "Water availability", errors);
        ValidateEnum(request.IrrigationAvailability, "Irrigation availability", errors);
        ValidateEnum(request.WaterReliability, "Water reliability", errors);
        ValidateEnum(request.DrainageCondition, "Drainage condition", errors);
        ValidateEnum(request.WaterloggingRisk, "Waterlogging risk", errors);
        ValidateEnum(request.GeneralFieldCondition, "General field condition", errors);
        ValidateEnum(request.PlantingReadiness, "Planting readiness", errors);
        ValidateOptionalText(request.SoilNotes, 1000, "Soil notes", errors);
        ValidateOptionalText(request.MainWaterSource, 240, "Main water source", errors);
        ValidateOptionalText(request.WaterConcerns, 1000, "Water concerns", errors);
        ValidateOptionalText(request.DrainageNotes, 1000, "Drainage notes", errors);
        ValidateOptionalText(request.GeneralFieldNotes, 1000, "General field notes", errors);
        ValidateOptionalText(request.RiskNotes, 1500, "Risk notes", errors);
        ValidateOptionalText(request.RisksAndConcerns, 1500, "Risks / concerns", errors);
        ValidateOptionalText(request.OfficerNotes, 2000, "Officer notes", errors);

        if (request.IdentifiedRisks is not null)
        {
            if (request.IdentifiedRisks.Any(risk => !Enum.IsDefined(risk)))
                errors.Add("Identified risks contain an invalid value.");
            if (request.IdentifiedRisks.Distinct().Count() != request.IdentifiedRisks.Count)
                errors.Add("Identified risks contain duplicate values.");
        }

        return errors;
    }

    private static void ValidateEnum<TEnum>(TEnum? value, string name, List<string> errors)
        where TEnum : struct, Enum
    {
        if (value.HasValue && !Enum.IsDefined(value.Value)) errors.Add($"{name} is invalid.");
    }

    private static void ValidateOptionalText(string? value, int maxLength, string name, List<string> errors)
    {
        if (value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > maxLength))
            errors.Add($"{name} must contain text and be {maxLength} characters or fewer when supplied.");
    }
}

public static class PrePlantingAssessmentRules
{
    public static IReadOnlyList<string> ValidateSubmission(PrePlantingAssessmentRequest request)
    {
        var errors = new List<string>(new PrePlantingAssessmentRequestValidator().Validate(request));
        Require(request.SoilType, "Soil type", errors);
        Require(request.SoilCondition, "Soil condition", errors);
        Require(request.SoilMoisture, "Soil moisture", errors);
        Require(request.WaterAvailability, "Water availability", errors);
        Require(request.IrrigationAvailability, "Irrigation availability", errors);
        Require(request.WaterReliability, "Water reliability", errors);
        Require(request.DrainageCondition, "Drainage condition", errors);
        Require(request.WaterloggingRisk, "Waterlogging risk", errors);
        Require(request.GeneralFieldCondition, "General field condition", errors);
        Require(request.PlantingReadiness, "Planting readiness", errors);

        if (request.IdentifiedRisks is null)
            errors.Add("Identified risks must be assessed before submission.");

        if (request.WaterAvailability is PrePlantingWaterAvailability.Adequate
            or PrePlantingWaterAvailability.Limited
            or PrePlantingWaterAvailability.Seasonal)
        {
            RequireText(request.MainWaterSource, "Main water source", errors);
        }

        if (request.WaterAvailability is PrePlantingWaterAvailability.Limited
            or PrePlantingWaterAvailability.Unavailable
            or PrePlantingWaterAvailability.Seasonal)
        {
            RequireText(request.WaterConcerns, "Water concerns", errors);
        }

        if (request.SoilType == PrePlantingSoilType.Other || request.SoilCondition == PrePlantingSoilCondition.Other)
            RequireText(request.SoilNotes, "Soil notes", errors);
        if (request.GeneralFieldCondition == PrePlantingGeneralFieldCondition.Other)
            RequireText(request.GeneralFieldNotes, "General field notes", errors);
        if (request.DrainageCondition == PrePlantingDrainageCondition.Poor
            || request.WaterloggingRisk is PrePlantingWaterloggingRisk.Moderate or PrePlantingWaterloggingRisk.High)
        {
            RequireText(request.DrainageNotes, "Drainage notes", errors);
        }

        var riskNotes = request.RiskNotes ?? request.RisksAndConcerns;
        if (request.IdentifiedRisks?.Contains(PrePlantingRisk.Other) == true)
            RequireText(riskNotes, "Risk notes", errors);

        return errors;
    }

    private static void Require<T>(T? value, string name, List<string> errors)
        where T : struct
    {
        if (!value.HasValue) errors.Add($"{name} is required before submission.");
    }

    private static void RequireText(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{name} is required before submission.");
    }
}
