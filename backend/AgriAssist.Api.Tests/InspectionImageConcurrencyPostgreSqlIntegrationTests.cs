using AgriAssist.Api.Data;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

/// <summary>
/// Exercises the production inspection-row lock ordering against PostgreSQL. These tests deliberately use
/// separate DbContexts so EF InMemory cannot hide partial-index or FOR UPDATE behavior.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class InspectionImageConcurrencyPostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING";

    [PostgreSqlFact]
    public async Task Representative_selection_and_submission_are_serialized_by_inspection_lock()
    {
        var data = await SeedAsync();
        var gate = Gate();

        async Task<bool> SelectAsync()
        {
            await gate.Task;
            await using var db = NewDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var inspection = await LockAsync(db, data.InspectionId);
            if (inspection.Status != InspectionStatus.InProgress) return false;
            var images = await db.InspectionImages.Where(item => item.FieldInspectionId == inspection.Id).ToListAsync();
            foreach (var image in images) image.IsRepresentativeForAi = image.Id == data.SecondImageId;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        async Task SubmitAsync()
        {
            await gate.Task;
            await using var db = NewDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var inspection = await LockAsync(db, data.InspectionId);
            inspection.Status = InspectionStatus.Completed;
            inspection.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var select = Task.Run(SelectAsync);
        var submit = Task.Run(SubmitAsync);
        gate.SetResult();
        await Task.WhenAll(select, submit);

        await using var verify = NewDbContext();
        Assert.Equal(InspectionStatus.Completed, await verify.FieldInspections.Where(item => item.Id == data.InspectionId).Select(item => item.Status).SingleAsync());
        Assert.True(await verify.InspectionImages.CountAsync(item => item.FieldInspectionId == data.InspectionId && item.IsRepresentativeForAi) <= 1);
    }

    [PostgreSqlFact]
    public async Task Review_creation_and_submission_freeze_have_one_authoritative_order()
    {
        var data = await SeedAsync(InspectionImageAnalysisStatus.Succeeded);
        var gate = Gate();

        async Task<Guid?> ReviewAsync()
        {
            await gate.Task;
            await using var db = NewDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var inspection = await LockAsync(db, data.InspectionId);
            if (inspection.Status != InspectionStatus.InProgress) return null;
            var review = new InspectionImageAnalysisReview
            {
                InspectionImageAnalysisId = data.AnalysisId!.Value,
                ReviewedByUserId = data.OfficerId,
                Disposition = ImageAnalysisReviewDisposition.Accepted,
                ReviewedProjectionJson = "{}",
                EditedFieldsJson = "[]"
            };
            db.InspectionImageAnalysisReviews.Add(review);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return review.Id;
        }

        async Task SubmitAsync()
        {
            await gate.Task;
            await using var db = NewDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var inspection = await LockAsync(db, data.InspectionId);
            var latest = await db.InspectionImageAnalysisReviews
                .Where(item => item.InspectionImageAnalysisId == data.AnalysisId)
                .OrderByDescending(item => item.ReviewedAt).ThenByDescending(item => item.Id)
                .FirstOrDefaultAsync();
            inspection.FrozenImageAnalysisReviewId = latest?.Disposition is ImageAnalysisReviewDisposition.Accepted or ImageAnalysisReviewDisposition.Edited
                ? latest.Id
                : null;
            inspection.Status = InspectionStatus.Completed;
            inspection.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var reviewTask = Task.Run(ReviewAsync);
        var submitTask = Task.Run(SubmitAsync);
        gate.SetResult();
        await Task.WhenAll(reviewTask, submitTask);

        await using var verify = NewDbContext();
        var inspection = await verify.FieldInspections.SingleAsync(item => item.Id == data.InspectionId);
        var reviews = await verify.InspectionImageAnalysisReviews.Where(item => item.InspectionImageAnalysisId == data.AnalysisId).ToListAsync();
        Assert.Equal(InspectionStatus.Completed, inspection.Status);
        Assert.True(reviews.Count <= 1);
        Assert.Equal(reviewTask.Result, inspection.FrozenImageAnalysisReviewId);
    }

    [PostgreSqlFact]
    public async Task Terminalization_after_submission_becomes_stale_instead_of_succeeded()
    {
        var data = await SeedAsync(InspectionImageAnalysisStatus.Running);
        var submissionHasLock = Gate();
        var allowSubmissionCommit = Gate();

        var submit = Task.Run(async () =>
        {
            await using var db = NewDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var inspection = await LockAsync(db, data.InspectionId);
            inspection.Status = InspectionStatus.Completed;
            inspection.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            submissionHasLock.SetResult();
            await allowSubmissionCommit.Task;
            await transaction.CommitAsync();
        });

        await submissionHasLock.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var terminalize = Task.Run(async () =>
        {
            await using var db = NewDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var inspection = await LockAsync(db, data.InspectionId);
            var analysis = await db.InspectionImageAnalyses.SingleAsync(item => item.Id == data.AnalysisId);
            analysis.Status = inspection.Status == InspectionStatus.InProgress
                ? InspectionImageAnalysisStatus.Succeeded
                : InspectionImageAnalysisStatus.Stale;
            analysis.CompletedAt = DateTime.UtcNow;
            analysis.Version++;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
        allowSubmissionCommit.SetResult();
        await Task.WhenAll(submit, terminalize);

        await using var verify = NewDbContext();
        Assert.Equal(InspectionImageAnalysisStatus.Stale, await verify.InspectionImageAnalyses.Where(item => item.Id == data.AnalysisId).Select(item => item.Status).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task Duplicate_analyze_start_for_one_fingerprint_creates_one_running_record()
    {
        var data = await SeedAsync();
        var gate = Gate();
        const string fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        async Task<bool> StartAsync()
        {
            await gate.Task;
            await using var db = NewDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await LockAsync(db, data.InspectionId);
            var existing = await db.InspectionImageAnalyses.SingleOrDefaultAsync(item =>
                item.InspectionImageId == data.FirstImageId && item.AnalysisFingerprint == fingerprint
                && (item.Status == InspectionImageAnalysisStatus.Running || item.Status == InspectionImageAnalysisStatus.Succeeded));
            if (existing is not null) return false;
            db.InspectionImageAnalyses.Add(NewAnalysis(data, fingerprint, InspectionImageAnalysisStatus.Running));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        var starts = new[] { Task.Run(StartAsync), Task.Run(StartAsync) };
        gate.SetResult();
        var results = await Task.WhenAll(starts);

        Assert.Single(results.Where(item => item));
        await using var verify = NewDbContext();
        Assert.Single(await verify.InspectionImageAnalyses.Where(item => item.InspectionImageId == data.FirstImageId && item.AnalysisFingerprint == fingerprint).ToListAsync());
    }

    private static async Task<Seeded> SeedAsync(InspectionImageAnalysisStatus? analysisStatus = null)
    {
        await using var db = NewDbContext();
        await db.Database.MigrateAsync();
        var farmer = new AppUser { FullName = "Concurrency Farmer", Email = $"image-farmer-{Guid.NewGuid():N}@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var officer = new AppUser { FullName = "Concurrency Officer", Email = $"image-officer-{Guid.NewGuid():N}@example.test", PasswordHash = "hash", Role = ApplicationRole.FieldOfficer, IsActive = true };
        var farm = new Farm { Name = "Image Concurrency Farm", Location = "North", TotalArea = 5, OwnerUser = farmer };
        var field = new Field { Farm = farm, Name = "Image Field", Area = 2, SoilType = "Loam", IsActive = true };
        var crop = new CropType { Name = $"Rice-{Guid.NewGuid():N}", IsActive = true };
        var plan = new CropPlanRequest { Farm = farm, Field = field, CropType = crop, RequestedByUser = farmer, PreferredStartDate = new DateOnly(2026, 10, 1), PreferredEndDate = new DateOnly(2027, 1, 1), Objective = "Concurrency test", Status = CropPlanRequestStatus.PreliminaryGenerated };
        var inspection = new FieldInspection { Field = field, CropPlanRequest = plan, InspectorUser = officer, Purpose = InspectionPurpose.PrePlanting, Status = InspectionStatus.InProgress, ScheduledAt = DateTime.UtcNow, Summary = "Draft" };
        var first = NewImage(inspection, "first", true);
        var second = NewImage(inspection, "second", false);
        db.AddRange(farmer, officer, farm, field, crop, plan, inspection, first, second);
        InspectionImageAnalysis? analysis = null;
        if (analysisStatus.HasValue)
        {
            analysis = NewAnalysis(new Seeded(inspection.Id, officer.Id, first.Id, second.Id, null), new string('b', 64), analysisStatus.Value);
            db.InspectionImageAnalyses.Add(analysis);
        }
        await db.SaveChangesAsync();
        return new Seeded(inspection.Id, officer.Id, first.Id, second.Id, analysis?.Id);
    }

    private static InspectionImage NewImage(FieldInspection inspection, string suffix, bool representative) => new()
    {
        FieldInspection = inspection,
        Url = $"https://example.test/{suffix}.jpg",
        PublicId = $"inspection/{suffix}-{Guid.NewGuid():N}",
        AssetId = Guid.NewGuid().ToString("N"),
        StorageVersion = 1,
        DeliveryType = "authenticated",
        ContentType = "image/jpeg",
        SizeBytes = 128,
        ContentSha256 = new string(representative ? '1' : '2', 64),
        IsRepresentativeForAi = representative
    };

    private static InspectionImageAnalysis NewAnalysis(Seeded data, string fingerprint, InspectionImageAnalysisStatus status) => new()
    {
        FieldInspectionId = data.InspectionId,
        InspectionImageId = data.FirstImageId,
        AnalysisFingerprint = fingerprint,
        Status = status,
        InputSnapshotJson = "{}",
        Provider = "openai",
        Model = "gpt-6-luna",
        SourcePolicyVersion = "1",
        SourcePolicyHash = new string('c', 64),
        SchemaVersion = 1,
        PromptContractVersion = 1,
        ImagePreprocessingVersion = 1,
        RelevanceRuleVersion = 1
    };

    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Task<FieldInspection> LockAsync(AppDbContext db, Guid inspectionId) =>
        db.FieldInspections.FromSqlInterpolated($@"SELECT * FROM ""FieldInspections"" WHERE ""Id"" = {inspectionId} FOR UPDATE")
            .SingleAsync();

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(RequiredConnectionString()).Options);

    private static string RequiredConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionVariable)
        ?? throw new InvalidOperationException($"{ConnectionVariable} is required.");

    private sealed record Seeded(Guid InspectionId, Guid OfficerId, Guid FirstImageId, Guid SecondImageId, Guid? AnalysisId);

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
                Skip = $"Set {ConnectionVariable} to run the PostgreSQL inspection image concurrency tests.";
        }
    }
}
