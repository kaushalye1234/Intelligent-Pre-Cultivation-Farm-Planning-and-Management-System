using System.Text.Json;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Services.TaskApproval;
using Microsoft.EntityFrameworkCore;
using static AgriAssist.Api.Tests.WeatherResourceTestData;

namespace AgriAssist.Api.Tests;

public sealed class SchedulingEvidenceBuilderTests
{
    [Fact]
    public async Task Nationwide_reference_matches_a_farm_with_a_district_and_district_reference_takes_precedence()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null, farmLocation: "Dambulla", farmDistrict: "Badulla");
        var variety = new CropVariety { CropTypeId = data.CropTypeId, Name = "MICH HY2", IsActive = true };
        db.Add(variety);
        var plan = await db.CropPlanRequests.Include(item => item.Farm).SingleAsync(item => item.Id == data.RequestId);
        plan.CropVarietyId = variety.Id;
        var nationwide = Profile(data.CropTypeId, variety.Name, "Sri Lanka (no subregion stated)", DateTime.UtcNow.AddDays(-2));
        nationwide.Stages.Add(Stage("Flowering"));
        db.Add(nationwide);
        await db.SaveChangesAsync();

        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync(item => item.Id == data.WorkflowId);
        var first = await SchedulingEvidenceBuilder.BuildAsync(db, workflow, plan, CancellationToken.None);
        Assert.Equal(nationwide.Id, first.ProfileId);
        Assert.Single(first.Stages);

        var district = Profile(data.CropTypeId, variety.Name, "Badulla", DateTime.UtcNow.AddDays(-3));
        district.Stages.Add(Stage("Planting"));
        db.Add(district);
        await db.SaveChangesAsync();

        var second = await SchedulingEvidenceBuilder.BuildAsync(db, workflow, plan, CancellationToken.None);
        Assert.Equal(district.Id, second.ProfileId);
        Assert.Equal("Planting", Assert.Single(second.Stages).StageName);
    }

    [Fact]
    public async Task Unpinned_scheduling_skips_a_rule_only_profile_when_a_verified_profile_has_stages()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null, farmLocation: "Dambulla", farmDistrict: "Badulla");
        var plan = await db.CropPlanRequests.Include(item => item.Farm).SingleAsync(item => item.Id == data.RequestId);
        var staged = Profile(data.CropTypeId, null, "Sri Lanka", DateTime.UtcNow.AddDays(-2));
        staged.Stages.Add(Stage("Planting"));
        var rulesOnly = Profile(data.CropTypeId, null, "Badulla", DateTime.UtcNow.AddDays(-1), SampleUreaRule);
        db.AddRange(staged, rulesOnly);
        await db.SaveChangesAsync();

        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync(item => item.Id == data.WorkflowId);
        var evidence = await SchedulingEvidenceBuilder.BuildAsync(db, workflow, plan, CancellationToken.None);

        Assert.Equal(staged.Id, evidence.ProfileId);
        Assert.Equal("Planting", Assert.Single(evidence.Stages).StageName);
    }

    [Fact]
    public async Task Pinned_rule_only_profile_keeps_its_identity_and_empty_stages_without_falling_back()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        var plan = await db.CropPlanRequests.Include(item => item.Farm).SingleAsync(item => item.Id == data.RequestId);
        var staged = Profile(data.CropTypeId, null, null, DateTime.UtcNow.AddDays(-2));
        staged.Stages.Add(Stage("Planting"));
        var rulesOnly = Profile(data.CropTypeId, null, null, DateTime.UtcNow.AddDays(-1), SampleUreaRule);
        db.AddRange(staged, rulesOnly);
        await db.SaveChangesAsync();

        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync(item => item.Id == data.WorkflowId);
        workflow.Steps.Single(item => item.AgentName == "WeatherResourceAgent").Status = AgriAssist.Api.Models.Shared.AgentStepStatus.Completed;
        workflow.Steps.Single(item => item.AgentName == "WeatherResourceAgent").OutputJson =
            JsonSerializer.Serialize(new { requirementSource = new { cropReferenceProfileId = rulesOnly.Id } });
        await db.SaveChangesAsync();

        var evidence = await SchedulingEvidenceBuilder.BuildAsync(db, workflow, plan, CancellationToken.None);

        Assert.Equal(rulesOnly.Id, evidence.ProfileId);
        Assert.Empty(evidence.Stages);
    }

    private static CropStageReference Stage(string name) => new()
    {
        StageName = name,
        Sequence = 1,
        TypicalMinDays = 5,
        TypicalMaxDays = 7,
        SourceName = "SAMPLE source (test fixture)"
    };
}
