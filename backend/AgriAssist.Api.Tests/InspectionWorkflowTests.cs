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
    public async Task Only_owning_field_officer_can_mutate_pre_planting_draft_and_generic_submit_is_blocked()
    {
        await using var db = NewDbContext();
        var data = await SeedFieldAsync(db);
        var inspection = new FieldInspection
        {
            FieldId = data.FieldId,
            InspectorUserId = data.OfficerId,
            Purpose = InspectionPurpose.PrePlanting,
            ScheduledAt = DateTime.UtcNow,
            Status = InspectionStatus.InProgress,
            Summary = "Linked pre-planting assessment."
        };
        db.FieldInspections.Add(inspection);
        await db.SaveChangesAsync();
        var adminService = NewService(db, ApplicationRole.Admin, data.OfficerId);

        var submitError = await Assert.ThrowsAsync<ApiException>(() => adminService.SubmitInspectionAsync(inspection.Id, CancellationToken.None));
        var uploadError = await Assert.ThrowsAsync<ApiException>(() => adminService.UploadImageAsync(inspection.Id, CreateFormFile(), CancellationToken.None));
        var observationError = await Assert.ThrowsAsync<ApiException>(() => adminService.CreateObservationAsync(
            new ObservationRequest(inspection.Id, "OfficerNotes", "Administrative edit."),
            CancellationToken.None));

        Assert.Equal("FIELD_OFFICER_REQUIRED", submitError.Code);
        Assert.Equal("FIELD_OFFICER_REQUIRED", uploadError.Code);
        Assert.Equal("FIELD_OFFICER_REQUIRED", observationError.Code);
        Assert.Empty(await db.InspectionImages.ToListAsync());
        Assert.Empty(await db.InspectionObservations.ToListAsync());

        var fieldOfficerService = NewService(db, ApplicationRole.FieldOfficer, data.OfficerId);
        var created = await fieldOfficerService.CreateObservationAsync(
            new ObservationRequest(inspection.Id, "OfficerNotes", "Owner draft note."),
            CancellationToken.None);
        Assert.Equal(inspection.Id, created.FieldInspectionId);

        var otherOfficerService = NewService(db, ApplicationRole.FieldOfficer, Guid.NewGuid());
        var ownerError = await Assert.ThrowsAsync<ApiException>(() => otherOfficerService.CreateObservationAsync(
            new ObservationRequest(inspection.Id, "OfficerNotes", "Other officer edit."),
            CancellationToken.None));
        var submitErrorForOwner = await Assert.ThrowsAsync<ApiException>(() =>
            fieldOfficerService.SubmitInspectionAsync(inspection.Id, CancellationToken.None));

        Assert.Equal("PREPLANT_ASSESSMENT_OWNER_REQUIRED", ownerError.Code);
        Assert.Equal("PREPLANT_DEDICATED_SUBMISSION_REQUIRED", submitErrorForOwner.Code);
        Assert.Equal(InspectionStatus.InProgress, (await db.FieldInspections.SingleAsync()).Status);
    }

    [Fact]
    public async Task Completed_pre_planting_assessment_rejects_all_generic_mutations()
    {
        await using var db = NewDbContext();
        var data = await SeedFieldAsync(db);
        var inspection = new FieldInspection
        {
            FieldId = data.FieldId,
            InspectorUserId = data.OfficerId,
            Purpose = InspectionPurpose.PrePlanting,
            ScheduledAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow,
            Status = InspectionStatus.Completed,
            Summary = "Submitted pre-planting assessment."
        };
        db.FieldInspections.Add(inspection);
        await db.SaveChangesAsync();
        var service = NewService(db, ApplicationRole.FieldOfficer, data.OfficerId);

        var update = await Assert.ThrowsAsync<ApiException>(() => service.UpdateInspectionAsync(
            inspection.Id,
            new FieldInspectionRequest(data.FieldId, DateTime.UtcNow, InspectionStatus.InProgress, "Reopen attempt."),
            CancellationToken.None));
        var observation = await Assert.ThrowsAsync<ApiException>(() => service.CreateObservationAsync(
            new ObservationRequest(inspection.Id, "OfficerNotes", "Late edit."), CancellationToken.None));
        var issue = await Assert.ThrowsAsync<ApiException>(() => service.CreateIssueAsync(
            new CropIssueRequest(inspection.Id, "Late issue", "Late evidence.", CropIssueSeverity.High, CropIssueStatus.Open),
            CancellationToken.None));
        var image = await Assert.ThrowsAsync<ApiException>(() => service.UploadImageAsync(
            inspection.Id, CreateFormFile(), CancellationToken.None));
        var close = await Assert.ThrowsAsync<ApiException>(() => service.CloseInspectionAsync(
            inspection.Id, CancellationToken.None));

        Assert.All([update, observation, issue, image, close], error => Assert.Equal("PREPLANT_ASSESSMENT_IMMUTABLE", error.Code));
        Assert.Equal(InspectionStatus.Completed, (await db.FieldInspections.SingleAsync()).Status);
        Assert.Empty(await db.InspectionObservations.ToListAsync());
        Assert.Empty(await db.CropIssues.ToListAsync());
        Assert.Empty(await db.InspectionImages.ToListAsync());
    }

    [Fact]
    public async Task Routine_inspection_behavior_remains_available_to_existing_roles()
    {
        await using var db = NewDbContext();
        var data = await SeedFieldAsync(db);
        var fieldOfficer = NewService(db, ApplicationRole.FieldOfficer, data.OfficerId);
        var inspection = await fieldOfficer.CreateInspectionAsync(
            new FieldInspectionRequest(data.FieldId, DateTime.UtcNow, InspectionStatus.InProgress, "Routine inspection."),
            CancellationToken.None);
        var admin = NewService(db, ApplicationRole.Admin, data.OfficerId);

        await admin.CreateObservationAsync(
            new ObservationRequest(inspection.Id, "Routine", "Administrative routine note."),
            CancellationToken.None);
        var submitted = await admin.SubmitInspectionAsync(inspection.Id, CancellationToken.None);
        var farmer = NewService(db, ApplicationRole.Farmer, data.FarmerId);
        var visible = await farmer.GetInspectionAsync(inspection.Id, CancellationToken.None);

        Assert.Equal(InspectionStatus.Completed, submitted.Status);
        Assert.Single(visible.Observations);
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
