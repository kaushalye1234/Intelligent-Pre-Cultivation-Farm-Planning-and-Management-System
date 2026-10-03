namespace AgriAssist.Api.Dtos.CropPlanning;

public static class InspectionNoteAssistanceContract
{
    public const int Version = 1;
}

public sealed record InspectionNoteAssistanceRequest
{
    public PrePlantingSoilType? SoilType { get; init; }
    public PrePlantingSoilCondition? SoilCondition { get; init; }
    public PrePlantingSoilMoisture? SoilMoisture { get; init; }
    public PrePlantingWaterAvailability? WaterAvailability { get; init; }
    public string? MainWaterSource { get; init; }
    public PrePlantingIrrigationAvailability? IrrigationAvailability { get; init; }
    public PrePlantingWaterReliability? WaterReliability { get; init; }
    public PrePlantingDrainageCondition? DrainageCondition { get; init; }
    public PrePlantingWaterloggingRisk? WaterloggingRisk { get; init; }
    public PrePlantingGeneralFieldCondition? GeneralFieldCondition { get; init; }
    public PrePlantingPlantingReadiness? PlantingReadiness { get; init; }
    public IReadOnlyList<PrePlantingRisk> IdentifiedRisks { get; init; } = [];
    public string? SoilNotes { get; init; }
    public string? WaterConcerns { get; init; }
    public string? DrainageNotes { get; init; }
    public string? GeneralFieldNotes { get; init; }
    public string? RiskNotes { get; init; }
    public string? OfficerNotes { get; init; }
}

public sealed record InspectionNoteAssistanceAiInput(
    int ContractVersion,
    string CropName,
    string? VarietyName,
    string FieldName,
    string? FieldSoilType,
    InspectionNoteAssistanceRequest Draft);

public sealed record InspectionNoteSuggestions(
    string? SoilNotes,
    string? WaterConcerns,
    string? DrainageNotes,
    string? GeneralFieldNotes,
    string? RiskNotes,
    string? OfficerNotes);

public sealed record InspectionNoteAssistanceResponse(
    int ContractVersion,
    string Status,
    InspectionNoteSuggestions? Suggestions,
    IReadOnlyList<string> ContradictionWarnings,
    IReadOnlyList<string> MissingDataWarnings,
    string? FailureCategory);

