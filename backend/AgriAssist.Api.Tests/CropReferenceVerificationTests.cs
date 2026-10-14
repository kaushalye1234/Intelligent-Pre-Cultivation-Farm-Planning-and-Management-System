using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;
using static AgriAssist.Api.Tests.WeatherResourceTestData;

namespace AgriAssist.Api.Tests;

public sealed class CropReferenceVerificationTests
{
    [Theory]
    [InlineData("missing-stage", "REFERENCE_EVIDENCE_INCOMPLETE")]
    [InlineData("missing-rule", "REFERENCE_EVIDENCE_INCOMPLETE")]
    [InlineData("missing-source", "REFERENCE_EVIDENCE_INCOMPLETE")]
    [InlineData("missing-range", "REFERENCE_EVIDENCE_INCOMPLETE")]
    [InlineData("duplicate-sequence", "REFERENCE_EVIDENCE_INCOMPLETE")]
    [InlineData("resource-unit", "REFERENCE_RESOURCE_MISMATCH")]
    [InlineData("ambiguous-resource", "REFERENCE_RESOURCE_MISMATCH")]
    [InlineData("wrong-region", "REFERENCE_REGION_MISMATCH")]
    [InlineData("wrong-field", "FIELD_NOT_FOUND")]
    public async Task Incomplete_evidence_never_activates_a_draft(string defect, string expectedCode)
    {
        await using var db = NewDbContext();
        var seed = await SeedAsync(db);
        var reference = await PrepareDraft(db, seed.CropTypeId);
        if (defect == "missing-stage") { db.RemoveRange(reference.Stages); reference.Stages.Clear(); }
        if (defect == "missing-rule") { db.RemoveRange(reference.Rules); reference.Rules.Clear(); }
        if (defect == "missing-source") reference.SourceUrl = null;
        if (defect == "missing-range") reference.Stages[0].TypicalMaxDays = null;
        if (defect == "duplicate-sequence")
        {
            var stage = new CropStageReference { CropReferenceProfileId = reference.Id, StageName = "Duplicate", Sequence = 1,
                TypicalMinDays = 1, TypicalMaxDays = 2, SourceName = "SYNTHETIC SOURCE", SourceUrl = "https://example.test/source" };
            db.Add(stage);
        }
        if (defect == "resource-unit") (await db.Resources.SingleAsync()).Unit = "litre";
        if (defect == "ambiguous-resource")
        {
            var resource = await db.Resources.SingleAsync();
            db.Add(new Resource { Name = resource.Name, Unit = resource.Unit, ResourceCategoryId = resource.ResourceCategoryId });
        }
        if (defect == "wrong-region") reference.Region = "Jaffna";
        await db.SaveChangesAsync();
        var service = CreateService(db, Guid.NewGuid());

        var error = await Assert.ThrowsAsync<ApiException>(() => service.VerifyAsync(reference.Id,
            new VerifyReferenceRequest(defect == "wrong-field" ? Guid.NewGuid() : seed.FieldId, WaterRegime.Irrigated,
                "Synthetic officer source and field review", reference.DraftVersion, true), CancellationToken.None));

        Assert.Equal(expectedCode, error.Code);
        Assert.False(reference.IsActive);
        Assert.Null(reference.VerifiedAt);
        Assert.Empty(await db.FieldWaterRegimeVerifications.ToListAsync());
    }

    internal static async Task<CropReferenceProfile> PrepareDraft(AppDbContext db, Guid cropTypeId, Guid? resourceId = null)
    {
        var reference = await db.CropReferenceProfiles.Include(item => item.Rules).SingleAsync(item => item.CropTypeId == cropTypeId);
        reference.VerificationState = CropReferenceVerificationState.Draft;
        reference.VerifiedAt = null;
        reference.VerifiedByUserId = null;
        reference.IsActive = false;
        reference.SourceUrl = "https://example.test/source";
        var stage = new CropStageReference { CropReferenceProfileId = reference.Id, StageName = "Planting", Sequence = 1,
            TypicalMinDays = 3, TypicalMaxDays = 5, SourceName = "SYNTHETIC SOURCE", SourceUrl = "https://example.test/source" };
        reference.Stages.Add(stage);
        db.Add(stage);
        foreach (var rule in reference.Rules) { rule.SourceUrl = reference.SourceUrl; rule.VerifiedAt = default; }
        if (resourceId.HasValue)
            reference.Rules[0].StructuredValueJson = System.Text.Json.JsonSerializer.Serialize(new {
                resourceId = resourceId.Value, resourceName = "Urea", quantityPerArea = 100, resourceUnit = "kg", areaUnit = "acre"
            });
        await db.SaveChangesAsync();
        return reference;
    }

    internal static CropReferenceVerificationService CreateService(AppDbContext db, Guid officerId)
    {
        var user = new Officer(officerId);
        var cropPlanning = new CropPlanningService(db, user, new FarmRequestValidator(), new FieldRequestValidator(),
            new CropTypeRequestValidator(), new CropCycleRequestValidator(), new CropPlanRequestCreateValidator(),
            new CropPlanRequestUpdateValidator(), new UnusedClient());
        return new CropReferenceVerificationService(db, user, cropPlanning);
    }

    private sealed class Officer(Guid id) : ICurrentUserService
    {
        public Guid? UserId => id;
        public ApplicationRole? Role => ApplicationRole.AgriculturalOfficer;
        public bool IsInRole(ApplicationRole role) => role == Role;
    }
    private sealed class UnusedClient : IAgenticAIClient
    {
        public Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
