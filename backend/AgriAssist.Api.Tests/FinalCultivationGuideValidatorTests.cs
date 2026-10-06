using AgriAssist.Api.Dtos.FinalCultivationGuide;
using AgriAssist.Api.Services.FinalCultivationGuide;

namespace AgriAssist.Api.Tests;

public sealed class FinalCultivationGuideValidatorTests
{
    private static readonly Guid WorkflowId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Validate_accepts_matching_revision_and_advice_without_numeric_values()
    {
        var input = Input();
        var output = Output();

        var result = FinalCultivationGuideValidator.Validate(input, output);

        Assert.Same(output, result);
    }

    [Fact]
    public void Validate_rejects_output_for_another_approved_revision()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            FinalCultivationGuideValidator.Validate(Input(), Output(approvedRevision: 1)));

        Assert.Contains("workflow or revision", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_numeric_advice()
    {
        var output = Output() with { WeeklyGuidance = ["Apply 25 kilograms of fertilizer."] };

        var error = Assert.Throws<InvalidOperationException>(() =>
            FinalCultivationGuideValidator.Validate(Input(), output));

        Assert.Contains("numeric", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_month_outside_the_preferred_window()
    {
        var output = Output() with
        {
            MonthlyGuidance = [new FinalGuideMonthDto("2027-04", "Support healthy growth.", [], [])]
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            FinalCultivationGuideValidator.Validate(Input(), output));

        Assert.Contains("outside", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_removes_current_stage_without_explicit_verified_stage_evidence()
    {
        var output = Output() with { CurrentStageExplanation = "The crop is in early establishment." };

        var result = FinalCultivationGuideValidator.Validate(Input(), output);

        Assert.Null(result.CurrentStageExplanation);
    }

    private static FinalCultivationGuideInputDto Input() => new(
        1, WorkflowId, 2, Guid.Parse("22222222-2222-2222-2222-222222222222"), "Maize", null, "Farm A", "Field A", "Kurunegala",
        new DateOnly(2026, 10, 1), new DateOnly(2027, 2, 28), new DateOnly(2026, 10, 5), [], []);

    private static FinalCultivationGuideOutputDto Output(int approvedRevision = 2) => new(
        1, WorkflowId, approvedRevision, ["Check for standing water."], null,
        [new FinalGuideMonthDto("2026-11", "Support healthy growth.", [], [])], [],
        ["Keep the harvest area clear."], "This guide uses the approved crop and field information.");
}
