namespace AgriAssist.Api.Dtos.FinalCultivationGuide;

public sealed record FinalGuideActivityDto(
    Guid Id,
    string Kind,
    string Title,
    DateTime? ScheduledAt,
    decimal? Quantity = null,
    string? Unit = null,
    int? DurationMinutes = null);

public sealed record FinalCultivationGuideInputDto(
    int ContractVersion,
    Guid WorkflowId,
    int ApprovedRevision,
    Guid CropPlanRequestId,
    string CropName,
    string? VarietyName,
    string? FarmName,
    string? FieldName,
    string? Location,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    DateOnly CurrentDate,
    IReadOnlyList<string> EvidenceSummary,
    IReadOnlyList<FinalGuideActivityDto> ApprovedActivities);

public sealed record FinalGuideMonthDto(
    string Month,
    string Summary,
    IReadOnlyList<string> FieldAdvice,
    IReadOnlyList<string> WeatherAdvice);

public sealed record FinalCultivationGuideOutputDto(
    int ContractVersion,
    Guid WorkflowId,
    int ApprovedRevision,
    IReadOnlyList<string> WeeklyGuidance,
    string? CurrentStageExplanation,
    IReadOnlyList<FinalGuideMonthDto> MonthlyGuidance,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> HarvestPreparation,
    string WhyThisPlan)
{
    public IReadOnlyList<FinalGuideActivityDto> ApprovedActivities { get; init; } = [];
}

public sealed record FinalCultivationGuideStatusDto(
    string Status,
    int ApprovedRevision,
    FinalCultivationGuideOutputDto? Guide);
