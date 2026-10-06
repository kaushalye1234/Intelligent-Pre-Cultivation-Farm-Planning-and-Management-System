using System.Globalization;
using System.Text.RegularExpressions;
using AgriAssist.Api.Dtos.FinalCultivationGuide;

namespace AgriAssist.Api.Services.FinalCultivationGuide;

public static partial class FinalCultivationGuideValidator
{
    public static FinalCultivationGuideOutputDto Validate(
        FinalCultivationGuideInputDto input,
        FinalCultivationGuideOutputDto output)
    {
        if (output.ContractVersion != 1 || output.WorkflowId != input.WorkflowId || output.ApprovedRevision != input.ApprovedRevision)
            throw new InvalidOperationException("Guide output workflow or revision does not match the approved input.");

        var narratives = output.WeeklyGuidance
            .Append(output.CurrentStageExplanation ?? string.Empty)
            .Append(output.WhyThisPlan)
            .Concat(output.Risks)
            .Concat(output.HarvestPreparation)
            .Concat(output.MonthlyGuidance.SelectMany(month => new[] { month.Summary }.Concat(month.FieldAdvice).Concat(month.WeatherAdvice)));
        if (narratives.Any(value => DigitRegex().IsMatch(value)))
            throw new InvalidOperationException("Guide contains numeric guidance that is not authoritative.");

        foreach (var month in output.MonthlyGuidance)
        {
            if (!DateOnly.TryParseExact($"{month.Month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var firstDay)
                || firstDay < new DateOnly(input.PreferredStartDate.Year, input.PreferredStartDate.Month, 1)
                || firstDay > new DateOnly(input.PreferredEndDate.Year, input.PreferredEndDate.Month, 1))
                throw new InvalidOperationException("Guide month is outside the preferred cultivation window.");
        }

        if (!string.IsNullOrWhiteSpace(output.CurrentStageExplanation)
            && !input.EvidenceSummary.Any(summary =>
                summary.Contains("current stage", StringComparison.OrdinalIgnoreCase)))
        {
            output = output with { CurrentStageExplanation = null };
        }

        return output;
    }

    [GeneratedRegex(@"\d")]
    private static partial Regex DigitRegex();
}
