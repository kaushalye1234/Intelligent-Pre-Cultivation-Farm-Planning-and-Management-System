using System.Security.Cryptography;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.ExternalServices.Cloudinary;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Inspections;
using AgriAssist.Api.Services.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgriAssist.Api.Tests;

public sealed class InspectionImageAnalysisServiceTests
{
    [Fact]
    public async Task Analyze_terminalizes_once_and_accept_review_is_append_only()
    {
        await using var db = NewDbContext();
        var seeded = await SeedAsync(db);
        var ai = new FakeImageAiClient();
        var service = NewService(db, seeded.OfficerId, ai);

        var state = await service.AnalyzeAsync(seeded.PlanId, CancellationToken.None);
        var review = await service.ReviewAsync(seeded.PlanId, new InspectionImageAnalysisReviewRequest(
            ImageAnalysisReviewDecision.Accepted, null, null), CancellationToken.None);

        Assert.Equal("Succeeded", state.Status);
        Assert.True(state.IsReviewable);
        Assert.Equal(1, ai.RunCount);
        var stored = await db.InspectionImageAnalyses.SingleAsync();
        Assert.Equal(InspectionImageAnalysisStatus.Succeeded, stored.Status);
        Assert.NotNull(stored.Pass1ResultJson);
        Assert.NotNull(stored.FinalResultJson);
        Assert.Equal(ImageAnalysisReviewDecision.Accepted, review.Disposition);
        Assert.Single(await db.InspectionImageAnalysisReviews.ToListAsync());
        Assert.Equal(CropHealthActionType.MonitorSymptoms, review.Projection!.Actions[0].ActionType);
        Assert.Equal(Member2CropHealthContractVersions.ReviewedProjection, review.Projection.ContractVersion);
    }

    [Fact]
    public async Task Staff_audit_history_is_explicit_and_remains_inspection_scoped()
    {
        await using var db = NewDbContext();
        var seeded = await SeedAsync(db);
        var service = NewService(db, seeded.OfficerId, new FakeImageAiClient());
        await service.AnalyzeAsync(seeded.PlanId, CancellationToken.None);
        await service.ReviewAsync(seeded.PlanId, new InspectionImageAnalysisReviewRequest(
            ImageAnalysisReviewDecision.Accepted, null, null), CancellationToken.None);

        var history = await service.GetHistoryAsync(seeded.PlanId, CancellationToken.None);

        var item = Assert.Single(history);
        Assert.True(item.IsCurrent);
        Assert.False(item.IsFrozen);
        Assert.NotNull(item.Result);
        Assert.Single(item.Reviews);
        await Assert.ThrowsAsync<ApiException>(() => NewService(db, Guid.NewGuid(), new FakeImageAiClient())
            .GetHistoryAsync(seeded.PlanId, CancellationToken.None));
        await Assert.ThrowsAsync<ApiException>(() => NewService(db, seeded.FarmerId, new FakeImageAiClient(), ApplicationRole.ResourceOfficer)
            .GetHistoryAsync(seeded.PlanId, CancellationToken.None));
    }

    [Fact]
    public async Task Provider_unavailable_does_not_break_manual_state_or_create_running_record()
    {
        await using var db = NewDbContext();
        var seeded = await SeedAsync(db);
        var service = NewService(db, seeded.OfficerId, new FakeImageAiClient(capabilityAvailable: false));

        var state = await service.AnalyzeAsync(seeded.PlanId, CancellationToken.None);

        Assert.Equal("Unavailable", state.Status);
        Assert.Empty(await db.InspectionImageAnalyses.ToListAsync());
        Assert.Equal(InspectionStatus.InProgress, (await db.FieldInspections.SingleAsync()).Status);
    }

    [Fact]
    public async Task Completion_during_paid_operation_terminalizes_directly_as_stale()
    {
        await using var db = NewDbContext();
        var seeded = await SeedAsync(db);
        var ai = new FakeImageAiClient(onRun: async () =>
        {
            var inspection = await db.FieldInspections.SingleAsync();
            inspection.Status = InspectionStatus.Completed;
            inspection.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        var service = NewService(db, seeded.OfficerId, ai);

        var state = await service.AnalyzeAsync(seeded.PlanId, CancellationToken.None);

        Assert.Equal("Stale", state.Status);
        Assert.False(state.IsReviewable);
        Assert.Equal(InspectionImageAnalysisStatus.Stale, (await db.InspectionImageAnalyses.SingleAsync()).Status);
    }

    private static InspectionImageAnalysisService NewService(
        AppDbContext db,
        Guid userId,
        IInspectionImageAnalysisAIClient ai,
        ApplicationRole role = ApplicationRole.FieldOfficer) =>
        new(
            db,
            new CurrentUserStub(userId, role),
            new FakeCloudinaryService(),
            ai,
            NullLogger<InspectionImageAnalysisService>.Instance);

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Seeded> SeedAsync(AppDbContext db)
    {
        var farmer = new AppUser { FullName = "Farmer", Email = $"farmer-{Guid.NewGuid()}@test.local", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var officer = new AppUser { FullName = "Officer", Email = $"officer-{Guid.NewGuid()}@test.local", PasswordHash = "hash", Role = ApplicationRole.FieldOfficer, IsActive = true };
        var farm = new Farm { Name = "Farm", Location = "Kandy", TotalArea = 5, OwnerUser = farmer };
        var field = new Field { Name = "North", Farm = farm, Area = 2, SoilType = "Loam", IsActive = true };
        var crop = new CropType { Name = "Rice", IsActive = true };
        var variety = new CropVariety { Name = "Bg 352", CropType = crop, IsActive = true };
        var plan = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = crop,
            CropVariety = variety,
            RequestedByUser = farmer,
            PreferredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            PreferredEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(100)),
            Objective = "Rice plan",
            Status = CropPlanRequestStatus.Submitted
        };
        var inspection = new FieldInspection
        {
            Field = field,
            CropPlanRequest = plan,
            InspectorUser = officer,
            Purpose = InspectionPurpose.PrePlanting,
            Status = InspectionStatus.InProgress,
            ScheduledAt = DateTime.UtcNow,
            Summary = "Draft"
        };
        var bytes = FakeCloudinaryService.Bytes;
        var image = new InspectionImage
        {
            FieldInspection = inspection,
            PublicId = "inspection/image",
            AssetId = "asset-1",
            StorageVersion = 1,
            DeliveryType = "authenticated",
            Url = "https://cloudinary.test/v1/inspection/image",
            ContentType = "image/jpeg",
            SizeBytes = bytes.Length,
            ContentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            IsRepresentativeForAi = true
        };
        db.AddRange(farmer, officer, farm, field, crop, variety, plan, inspection, image);
        await db.SaveChangesAsync();
        return new Seeded(farmer.Id, officer.Id, plan.Id);
    }

    private sealed record Seeded(Guid FarmerId, Guid OfficerId, Guid PlanId);

    private sealed class CurrentUserStub(Guid userId, ApplicationRole role) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => roleToCheck == role;
    }

    private sealed class FakeCloudinaryService : ICloudinaryService
    {
        public static readonly byte[] Bytes = [0xff, 0xd8, 0xff, 0xd9];
        public Task<CloudinaryUploadResult> UploadInspectionImageAsync(IFormFile file, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CloudinaryRetrievedAsset> RetrieveInspectionImageAsync(string publicId, long? storageVersion, string deliveryType, CancellationToken cancellationToken) =>
            Task.FromResult(new CloudinaryRetrievedAsset(Bytes, "image/jpeg"));
    }

    private sealed class FakeImageAiClient(bool capabilityAvailable = true, Func<Task>? onRun = null) : IInspectionImageAnalysisAIClient
    {
        public int RunCount { get; private set; }

        public Task<ImageAnalysisCapabilityResponse> GetImageAnalysisCapabilityAsync(CancellationToken cancellationToken) =>
            capabilityAvailable
                ? Task.FromResult(new ImageAnalysisCapabilityResponse(1, 1, 1, 1, "test-policy", new string('a', 64), "openai", "gpt-6-luna"))
                : throw new HttpRequestException("unavailable");

        public async Task<InspectionImageAnalysisAiResponse> RunImageAnalysisAsync(InspectionImageAnalysisAiInput input, CancellationToken cancellationToken)
        {
            RunCount++;
            if (onRun is not null) await onRun();
            return new InspectionImageAnalysisAiResponse(
                1,
                "Succeeded",
                new { contractVersion = 1, visibleFindings = new[] { "Yellowing is visible." } },
                [],
                new InspectionImageAnalysisFinalResult(
                    1,
                    ["Yellowing is visible."],
                    CropHealthIssueCategory.NutrientStress,
                    ["Possible nutrient-stress-like symptoms"],
                    "Moderate",
                    "The cause cannot be determined from one image.",
                    [],
                    [CropHealthActionType.MonitorSymptoms, CropHealthActionType.RequestFurtherAssessment],
                    true,
                    "Unavailable"),
                null,
                null);
        }
    }
}
