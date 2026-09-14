using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.ExternalServices.Cloudinary;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Inspections;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Inspections;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class InspectionWorkflowTests
{
    [Fact]
    public async Task Field_officer_can_create_submit_and_read_history()
    {
        await using var db = NewDbContext();
        var data = await SeedFieldAsync(db);
        var service = NewService(db, ApplicationRole.FieldOfficer, data.OfficerId);

        var inspection = await service.CreateInspectionAsync(new FieldInspectionRequest(data.FieldId, DateTime.UtcNow, InspectionStatus.InProgress, "Mobile GPS inspection."), CancellationToken.None);
        await service.CreateObservationAsync(new ObservationRequest(inspection.Id, "Growth", "Healthy stand with one wet patch."), CancellationToken.None);
        var submitted = await service.SubmitInspectionAsync(inspection.Id, CancellationToken.None);
        var history = await service.GetInspectionHistoryAsync(inspection.Id, CancellationToken.None);

        Assert.Equal(InspectionStatus.Completed, submitted.Status);
        Assert.NotNull(submitted.CompletedAt);
        Assert.Contains(history, item => item.EventType == "Observation");
        Assert.Contains(history, item => item.EventType == "InspectionSubmitted");
    }

    [Fact]
    public async Task Farmer_cannot_create_inspection()
    {
        await using var db = NewDbContext();
        var data = await SeedFieldAsync(db);
        var service = NewService(db, ApplicationRole.Farmer, data.FarmerId);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            service.CreateInspectionAsync(new FieldInspectionRequest(data.FieldId, DateTime.UtcNow, InspectionStatus.Scheduled, "Farmer attempt."), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal("INSPECTION_STAFF_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task Escalate_requires_serious_issue_and_marks_high_issue()
    {
        await using var db = NewDbContext();
        var data = await SeedFieldAsync(db);
        var service = NewService(db, ApplicationRole.FieldOfficer, data.OfficerId);
        var inspection = await service.CreateInspectionAsync(new FieldInspectionRequest(data.FieldId, DateTime.UtcNow, InspectionStatus.InProgress, "Issue inspection."), CancellationToken.None);
        var low = await service.CreateIssueAsync(new CropIssueRequest(inspection.Id, "Minor weed", "Small patch.", CropIssueSeverity.Low, CropIssueStatus.Open), CancellationToken.None);
        var high = await service.CreateIssueAsync(new CropIssueRequest(inspection.Id, "Leaf yellowing", "Spreading yellowing.", CropIssueSeverity.High, CropIssueStatus.Open), CancellationToken.None);

        var lowError = await Assert.ThrowsAsync<ApiException>(() => service.EscalateIssueAsync(low.Id, CancellationToken.None));
        var escalated = await service.EscalateIssueAsync(high.Id, CancellationToken.None);

        Assert.Equal("ISSUE_NOT_SERIOUS", lowError.Code);
        Assert.Equal(CropIssueStatus.Escalated, escalated.Status);
        Assert.NotNull(escalated.EscalatedAt);
    }

    [Fact]
    public async Task Upload_image_persists_cloudinary_metadata()
    {
        await using var db = NewDbContext();
        var data = await SeedFieldAsync(db);
        var service = NewService(db, ApplicationRole.FieldOfficer, data.OfficerId);
        var inspection = await service.CreateInspectionAsync(new FieldInspectionRequest(data.FieldId, DateTime.UtcNow, InspectionStatus.InProgress, "Image inspection."), CancellationToken.None);

        var image = await service.UploadImageAsync(inspection.Id, CreateFormFile(), CancellationToken.None);
        var images = await service.GetInspectionImagesAsync(inspection.Id, CancellationToken.None);

        Assert.Equal("agriassist/inspection.jpg", image.PublicId);
        Assert.Single(images);
        Assert.Equal("image/jpeg", images[0].ContentType);
    }

    [Fact]
    public async Task Cloudinary_failure_does_not_persist_image_metadata()
    {
        await using var db = NewDbContext();
        var data = await SeedFieldAsync(db);
        var service = NewService(db, ApplicationRole.FieldOfficer, data.OfficerId, new ThrowingCloudinaryService());
        var inspection = await service.CreateInspectionAsync(new FieldInspectionRequest(data.FieldId, DateTime.UtcNow, InspectionStatus.InProgress, "Image inspection."), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadImageAsync(inspection.Id, CreateFormFile(), CancellationToken.None));

        Assert.Empty(await db.InspectionImages.ToListAsync());
    }

    [Fact]
    public async Task Follow_up_can_be_marked_completed()
    {
        await using var db = NewDbContext();
        var data = await SeedFieldAsync(db);
        var service = NewService(db, ApplicationRole.FieldOfficer, data.OfficerId);
        var inspection = await service.CreateInspectionAsync(new FieldInspectionRequest(data.FieldId, DateTime.UtcNow, InspectionStatus.InProgress, "Follow-up inspection."), CancellationToken.None);
        var issue = await service.CreateIssueAsync(new CropIssueRequest(inspection.Id, "Leaf yellowing", "Needs review.", CropIssueSeverity.High, CropIssueStatus.Open), CancellationToken.None);
        var recommendation = await service.CreateRecommendationAsync(new FollowUpRecommendationRequest(issue.Id, "Reinspect in two days.", DateTime.UtcNow.AddDays(2), false), CancellationToken.None);

        var updated = await service.UpdateRecommendationAsync(recommendation.Id, new FollowUpRecommendationUpdateRequest(true), CancellationToken.None);

        Assert.True(updated.IsCompleted);
        var open = await service.SearchRecommendationsAsync(new PagedQuery(), null, false, CancellationToken.None);
        Assert.Empty(open.Items);
    }

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static InspectionService NewService(AppDbContext db, ApplicationRole role, Guid userId, ICloudinaryService? cloudinary = null) =>
        new(
            db,
            new CurrentUserStub(role, userId),
            cloudinary ?? new SuccessfulCloudinaryService(),
            new FieldInspectionRequestValidator(),
            new ObservationRequestValidator(),
            new CropIssueRequestValidator(),
            new FollowUpRecommendationRequestValidator());

    private static async Task<SeededField> SeedFieldAsync(AppDbContext db)
    {
        var farmer = new AppUser { FullName = "Farmer", Email = $"farmer-{Guid.NewGuid()}@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var officer = new AppUser { FullName = "Officer", Email = $"officer-{Guid.NewGuid()}@example.test", PasswordHash = "hash", Role = ApplicationRole.FieldOfficer, IsActive = true };
        var farm = new Farm { Name = "North Farm", Location = "North", TotalArea = 10, OwnerUser = farmer };
        var field = new Field { Name = "Field A", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
        db.AddRange(farmer, officer, farm, field);
        await db.SaveChangesAsync();
        return new SeededField(farmer.Id, officer.Id, field.Id);
    }

    private static FormFile CreateFormFile()
    {
        var stream = new MemoryStream([1, 2, 3, 4]);
        return new FormFile(stream, 0, stream.Length, "file", "inspection.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private sealed record SeededField(Guid FarmerId, Guid OfficerId, Guid FieldId);

    private sealed class CurrentUserStub(ApplicationRole role, Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }

    private sealed class SuccessfulCloudinaryService : ICloudinaryService
    {
        public Task<CloudinaryUploadResult> UploadInspectionImageAsync(IFormFile file, CancellationToken cancellationToken) =>
            Task.FromResult(new CloudinaryUploadResult("https://res.cloudinary.test/inspection.jpg", "agriassist/inspection.jpg", file.ContentType, file.Length));
    }

    private sealed class ThrowingCloudinaryService : ICloudinaryService
    {
        public Task<CloudinaryUploadResult> UploadInspectionImageAsync(IFormFile file, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Cloudinary unavailable.");
    }
}
