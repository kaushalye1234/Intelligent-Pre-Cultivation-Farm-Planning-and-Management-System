using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.TaskApproval;

/// <summary>Builds a bounded, source-attributed scheduling snapshot without changing upstream agent outputs.</summary>
public static class SchedulingEvidenceBuilder
{
    public static async Task<SchedulingEvidenceBundle> BuildAsync(
        AppDbContext dbContext,
        AgentWorkflow workflow,
        CropPlanRequest plan,
        CancellationToken cancellationToken)
    {
        AgentStep? Step(string agentName) => workflow.Steps
            .Where(item => item.AgentName == agentName && item.Status == AgentStepStatus.Completed)
            .OrderByDescending(item => item.Sequence)
            .ThenByDescending(item => item.CompletedAt)
            .FirstOrDefault();

        var coordinator = Step("CropPlanningCoordinatorAgent");
        var fieldAnalysis = Step("CropFieldAnalysisAgent");
        var weatherResource = Step("WeatherResourceAgent");
        var pinnedProfileId = ReadRequirementProfileId(weatherResource?.OutputJson);
        var varietyName = plan.CropVarietyId.HasValue
            ? await dbContext.CropVarieties.AsNoTracking()
                .Where(item => item.Id == plan.CropVarietyId.Value && !item.IsDeleted)
                .Select(item => item.Name)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var profiles = await dbContext.CropReferenceProfiles.AsNoTracking()
            .Include(item => item.Stages)
            .Include(item => item.Rules)
            .Where(item => item.CropTypeId == plan.CropTypeId && item.IsActive && !item.IsDeleted && item.VerifiedAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
        var compatibleProfiles = profiles
            .Where(item => item.VarietyName is null || string.Equals(item.VarietyName, varietyName, StringComparison.OrdinalIgnoreCase))
            .Where(item => CropReferenceRegionMatcher.Rank(item.Region, plan.Farm) >= 0)
            .ToArray();
        var profile = compatibleProfiles
            .Where(item => !pinnedProfileId.HasValue || item.Id == pinnedProfileId.Value)
            .Where(item => item.Stages.Any(stage => !stage.IsDeleted))
            .OrderByDescending(item => item.VarietyName is not null)
            .ThenByDescending(item => CropReferenceRegionMatcher.Rank(item.Region, plan.Farm))
            .ThenByDescending(item => item.VerifiedAt)
            .ThenBy(item => item.Id)
            .FirstOrDefault();
        // Preserve the pinned profile's identity when its only defect is
        // missing stages. The scheduler can report that precise dependency
        // while still refusing to produce an approvable candidate.
        if (profile is null && pinnedProfileId.HasValue)
            profile = compatibleProfiles.FirstOrDefault(item => item.Id == pinnedProfileId.Value);

        return new SchedulingEvidenceBundle(
            profile?.Id,
            profile?.SourceName,
            profile?.SourceUrl,
            profile?.SourceVersion,
            profile?.VerifiedAt,
            coordinator?.Id,
            fieldAnalysis?.Id,
            weatherResource?.Id,
            profile?.Stages.Where(item => !item.IsDeleted).OrderBy(item => item.Sequence).ThenBy(item => item.Id)
                .Select(item => new SchedulingStageEvidence(item.Id, item.StageName, item.Sequence,
                    item.TypicalMinDays, item.TypicalMaxDays, item.SourceName, item.SourceUrl)).ToArray() ?? [],
            profile?.Rules.Where(item => !item.IsDeleted &&
                    string.Equals(item.RuleType, IrrigationScheduleReferenceRule.RuleType, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.RuleKey).ThenBy(item => item.Id)
                .Select(item => IrrigationScheduleReferenceRule.TryParse(item.StructuredValueJson, out var parsed, out _)
                    ? new SchedulingIrrigationRuleEvidence(item.Id, item.RuleKey, parsed!.DayOffsetFromPlanting,
                        parsed.StartTimeUtc, parsed.DurationMinutes, item.SourceName, item.SourceUrl, item.VerifiedAt)
                    : null)
                .Where(item => item is not null)
                .Select(item => item!)
                .ToArray() ?? [],
            profile?.Rules.Where(item => !item.IsDeleted &&
                    string.Equals(item.RuleType, IrrigationScheduleReferenceRule.RuleType, StringComparison.OrdinalIgnoreCase))
                .Where(item => !IrrigationScheduleReferenceRule.TryParse(item.StructuredValueJson, out _, out _))
                .Select(item => item.Id).ToArray() ?? []);
    }

    private static Guid? ReadRequirementProfileId(string? outputJson)
    {
        if (string.IsNullOrWhiteSpace(outputJson)) return null;
        try
        {
            using var document = JsonDocument.Parse(outputJson);
            if (document.RootElement.TryGetProperty("requirementSource", out var source)
                && source.ValueKind != JsonValueKind.Null)
            {
                if (source.ValueKind == JsonValueKind.Object
                    && source.TryGetProperty("cropReferenceProfileId", out var id)
                    && id.ValueKind == JsonValueKind.String
                    && Guid.TryParse(id.GetString(), out var profileId)) return profileId;
                return Guid.Empty;
            }
        }
        catch (JsonException)
        {
            // Invalid upstream JSON must never cause fallback to an unrelated reference profile.
            return Guid.Empty;
        }
        return null;
    }

}
