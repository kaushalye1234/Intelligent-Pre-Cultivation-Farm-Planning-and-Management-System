using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using Microsoft.EntityFrameworkCore;
using AgriAssist.Api.Services.TaskApproval;
using System.Text.Json;
using static AgriAssist.Api.Tests.WeatherResourceTestData;

namespace AgriAssist.Api.Tests;

public sealed class PinnedCropReferenceTests
{
    [Fact]
    public async Task Required_profile_is_used_even_when_a_newer_legacy_profile_exists()
    {
        await using var db = NewDbContext();
        var seed = await SeedAsync(db);
        var reference = await db.CropReferenceProfiles.SingleAsync();
        reference.VerificationState = CropReferenceVerificationState.Verified;
        reference.VerifiedByUserId = Guid.NewGuid();
        reference.WaterRegime = WaterRegime.Irrigated;
        reference.FieldWaterRegimeVerification = new FieldWaterRegimeVerification
        {
            FieldId = seed.FieldId, WaterRegime = WaterRegime.Irrigated,
            VerifiedByUserId = reference.VerifiedByUserId.Value, VerifiedAt = DateTime.UtcNow,
            Observation = "Synthetic officer observation"
        };
        db.Add(reference.FieldWaterRegimeVerification);
        db.Add(Profile(seed.CropTypeId, null, null, DateTime.UtcNow, SampleUreaRule));
        await db.SaveChangesAsync();

        var result = await new CropResourceRequirementService(db, TestConfiguration())
            .GetRequirementsAsync(seed.RequestId, reference.Id, CancellationToken.None);

        Assert.Equal("Available", result.Status);
        Assert.Equal(reference.Id, result.Source!.CropReferenceProfileId);
    }

    [Theory]
    [InlineData("matching", true)]
    [InlineData("different-source", false)]
    [InlineData("missing-source", false)]
    [InlineData("inactive", false)]
    [InlineData("changed-version", false)]
    public async Task Scheduling_uses_the_same_required_source_and_verification_version(string condition, bool expected)
    {
        await using var db = NewDbContext();
        var seed = await SeedAsync(db);
        var reference = await db.CropReferenceProfiles.SingleAsync();
        reference.VerificationState = CropReferenceVerificationState.Verified;
        reference.VerifiedByUserId = Guid.NewGuid();
        reference.WaterRegime = WaterRegime.Irrigated;
        reference.FieldWaterRegimeVerification = new FieldWaterRegimeVerification {
            FieldId = seed.FieldId, WaterRegime = WaterRegime.Irrigated, Observation = "Synthetic field observation",
            VerifiedByUserId = reference.VerifiedByUserId.Value, VerifiedAt = DateTime.UtcNow
        };
        db.Add(reference.FieldWaterRegimeVerification);
        var stage = new CropStageReference { CropReferenceProfileId = reference.Id, StageName = "Maturity",
            Sequence = 1, TypicalMinDays = 98, TypicalMaxDays = 102, SourceName = "Synthetic" };
        db.Add(stage);
        var workflow = await db.AgentWorkflows.Include(item => item.Steps).SingleAsync(item => item.Id == seed.WorkflowId);
        workflow.RequiredCropReferenceProfileId = reference.Id;
        workflow.RequiredCropReferenceVersion = 1;
        if (condition == "inactive") reference.IsActive = false;
        if (condition == "changed-version") reference.DraftVersion = 2;
        var resourceStep = workflow.Steps.Single(item => item.AgentName == "WeatherResourceAgent");
        resourceStep.Status = AgentStepStatus.Completed;
        resourceStep.OutputJson = condition == "missing-source" ? "{}"
            : JsonSerializer.Serialize(new { requirementSource = new { cropReferenceProfileId = condition == "different-source" ? Guid.NewGuid() : reference.Id } },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await db.SaveChangesAsync();
        var plan = await db.CropPlanRequests.Include(item => item.Field).Include(item => item.Farm)
            .SingleAsync(item => item.Id == seed.RequestId);

        var evidence = await SchedulingEvidenceBuilder.BuildAsync(db, workflow, plan, CancellationToken.None);

        if (expected) { Assert.Equal(reference.Id, evidence.ProfileId); Assert.Single(evidence.Stages); }
        else { Assert.Null(evidence.ProfileId); Assert.Empty(evidence.Stages); }
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("unverified")]
    [InlineData("wrong-field")]
    [InlineData("wrong-regime")]
    [InlineData("missing")]
    public async Task Required_profile_never_falls_back_when_evidence_is_invalid(string defect)
    {
        await using var db = NewDbContext();
        var seed = await SeedAsync(db);
        var reference = Profile(seed.CropTypeId, null, null, DateTime.UtcNow.AddDays(-2), SampleUreaRule);
        reference.VerificationState = CropReferenceVerificationState.Verified;
        reference.VerifiedByUserId = Guid.NewGuid();
        reference.WaterRegime = WaterRegime.Irrigated;
        reference.FieldWaterRegimeVerification = new FieldWaterRegimeVerification
        {
            FieldId = defect == "wrong-field" ? Guid.NewGuid() : seed.FieldId,
            WaterRegime = defect == "wrong-regime" ? WaterRegime.Rainfed : WaterRegime.Irrigated,
            VerifiedByUserId = reference.VerifiedByUserId.Value, VerifiedAt = DateTime.UtcNow,
            Observation = "Synthetic officer observation"
        };
        if (defect == "inactive") reference.IsActive = false;
        if (defect == "unverified") reference.VerificationState = CropReferenceVerificationState.Draft;
        db.Add(reference);
        await db.SaveChangesAsync();

        var result = await new CropResourceRequirementService(db, TestConfiguration())
            .GetRequirementsAsync(seed.RequestId, defect == "missing" ? Guid.NewGuid() : reference.Id, CancellationToken.None);

        Assert.Equal("Unavailable", result.Status);
        Assert.Null(result.Source);
        Assert.Empty(result.Requirements);
    }
}
