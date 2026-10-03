using System.Text.Json;
using System.Text.Json.Serialization;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Services.Inspections;

namespace AgriAssist.Api.Tests;

public sealed class Member2ContractGoldenFixtureTests
{
    private static readonly JsonSerializerOptions StrictJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    [Fact]
    public void Valid_shared_fixtures_match_backend_contracts_and_versions()
    {
        var note = Read<InspectionNoteAssistanceResponse>("note-assistance.valid.json");
        var image = Read<InspectionImageAnalysisFinalResult>("image-analysis.valid.json");
        var reviewed = Read<ReviewedImageAnalysisProjection>("reviewed-projection.edited.valid.json");
        var field = Read<FieldAnalysisOutput>("field-analysis.valid.json");
        var consideration = Read<CropHealthWeatherResourceConsideration>("member3-consideration.valid.json");
        var proposal = Read<SchedulingValidationOutput>("member4-proposal.pending.valid.json");
        var legacyPlan = Read<FarmerApprovedPlanResponse>("farmer-approved-plan.legacy.valid.json");
        var cropHealthPlan = Read<FarmerApprovedPlanResponse>("farmer-approved-plan.crop-health.valid.json");

        Assert.Equal(InspectionNoteAssistanceContract.Version, note.ContractVersion);
        Assert.Equal(InspectionImageAnalysisContract.Version, image.ContractVersion);
        Assert.Equal(Member2CropHealthContractVersions.ReviewedProjection, reviewed.ContractVersion);
        Assert.Equal(Member2CropHealthContractVersions.CropFieldAnalysis, field.ContractVersion);
        Assert.Equal(Member2CropHealthContractVersions.Member3Consideration, consideration.ContractVersion);
        Assert.Equal(Member2CropHealthContractVersions.Member4Proposal, proposal.ContractVersion);
        Assert.Equal(Member2CropHealthContractVersions.FarmerApprovedPlan, legacyPlan.ContractVersion);
        Assert.Null(legacyPlan.CropHealth);
        Assert.NotNull(cropHealthPlan.CropHealth);
        Assert.Equal(CropHealthGuidanceDecision.PendingDecision, proposal.CropHealthGuidance!.Decision);
    }

    [Fact]
    public void Invalid_shared_fixtures_are_rejected_or_fail_version_validation()
    {
        Assert.Throws<JsonException>(() => Read<InspectionNoteAssistanceResponse>("note-assistance.invalid-unknown-field.json"));
        Assert.Throws<JsonException>(() => Read<InspectionImageAnalysisFinalResult>("image-analysis.invalid-action.json"));
        var unsupported = Read<FarmerApprovedPlanResponse>("farmer-approved-plan.invalid-version.json");
        Assert.NotEqual(Member2CropHealthContractVersions.FarmerApprovedPlan, unsupported.ContractVersion);
    }

    [Fact]
    public void Authoritative_action_catalog_is_complete_non_chemical_and_matches_mirrored_identifiers()
    {
        var enumValues = Enum.GetValues<CropHealthActionType>();
        Assert.Equal(enumValues.Order(), CropHealthActionCatalog.AllowedActionTypes.Order());
        var prohibited = new[] { "pesticide", "fungicide", "herbicide", "active ingredient", "dosage", "spray" };
        foreach (var action in enumValues)
        {
            var definition = CropHealthActionCatalog.Get(action);
            var semanticText = $"{definition.Title} {definition.Description}";
            Assert.DoesNotContain(prohibited, term => semanticText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static T Read<T>(string name) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(FixturePath(name)), StrictJson)
        ?? throw new InvalidOperationException($"Fixture {name} was empty.");

    private static string FixturePath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "ai-usage", "fixtures", "member2", name);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate shared Member 2 fixture {name}.");
    }
}
