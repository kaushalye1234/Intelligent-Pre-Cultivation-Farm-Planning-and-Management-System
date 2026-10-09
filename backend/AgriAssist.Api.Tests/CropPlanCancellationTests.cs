using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class CropPlanCancellationTests
{
    [Fact]
    public async Task Owning_farmer_can_cancel_active_plan_and_preserve_workflow_evidence()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, CropPlanRequestStatus.PreliminaryGenerated);
        var completedStep = new AgentStep
        {
            AgentWorkflow = data.Workflow,
            AgentName = "CropPlanningCoordinatorAgent",
            StepName = "CropPlanningCoordinator",
            Sequence = 1,
            Status = AgentStepStatus.Completed,
            OutputJson = "{\"status\":\"Planned\"}",
            CompletedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var pendingStep = new AgentStep
        {
            AgentWorkflow = data.Workflow,
            AgentName = "WeatherResourceAgent",
            StepName = "WeatherResourceAnalysis",
            Sequence = 2,
            Status = AgentStepStatus.Running,
            InputJson = "{\"evidence\":true}"
        };
        var inspection = new FieldInspection
        {
            CropPlanRequest = data.Request,
            Field = data.Field,
            InspectorUser = data.Officer,
            Purpose = InspectionPurpose.PrePlanting,
            Status = InspectionStatus.InProgress,
            ScheduledAt = DateTime.UtcNow,
            Summary = "Draft assessment"
        };
        var observation = new InspectionObservation
        {
            FieldInspection = inspection,
            ObservationType = "SoilCondition",
            Notes = "Moist loam"
        };
        db.AddRange(completedStep, pendingStep, inspection, observation);
        await db.SaveChangesAsync();
        var originalVersion = data.Workflow.Version;

        var response = await NewService(db, data.Farmer.Id, ApplicationRole.Farmer)
            .CancelCropPlanRequestAsync(data.Request.Id, new CropPlanCancellationRequest(null), CancellationToken.None);

        Assert.Equal(CropPlanRequestStatus.Cancelled, response.Status);
        Assert.Equal(AgentWorkflowStatus.Cancelled, data.Workflow.Status);
        Assert.Equal("Cancelled", data.Workflow.CurrentStep);
        Assert.Equal(originalVersion + 1, data.Workflow.Version);
        Assert.NotNull(data.Workflow.CompletedAt);
        Assert.Equal(AgentStepStatus.Completed, completedStep.Status);
        Assert.Equal("{\"status\":\"Planned\"}", completedStep.OutputJson);
        Assert.Equal(AgentStepStatus.Skipped, pendingStep.Status);
        Assert.Equal("CROP_PLAN_CANCELLED", pendingStep.ErrorCode);
        Assert.Equal(InspectionStatus.Cancelled, inspection.Status);
        Assert.Equal("Moist loam", (await db.InspectionObservations.SingleAsync()).Notes);

        var history = await db.CropPlanRequestHistories.SingleAsync();
        Assert.Equal(CropPlanHistoryAction.Cancelled, history.Action);
        Assert.Equal(ApplicationRole.Farmer, history.ChangedByRole);
        Assert.Equal("Cancelled by farmer.", history.Reason);
        Assert.Equal(data.Farmer.Id, history.ChangedByUserId);
    }

    [Fact]
    public async Task Admin_cancellation_requires_reason_and_records_role_snapshot()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, CropPlanRequestStatus.Submitted);
        var admin = await AddUserAsync(db, ApplicationRole.Admin, "admin@example.test");
        var service = NewService(db, admin.Id, ApplicationRole.Admin);

        var missing = await Assert.ThrowsAsync<ApiException>(() => service.CancelCropPlanRequestAsync(
            data.Request.Id, new CropPlanCancellationRequest("  "), CancellationToken.None));
        Assert.Equal("CROP_PLAN_CANCELLATION_REASON_REQUIRED", missing.Code);

        await service.CancelCropPlanRequestAsync(
            data.Request.Id, new CropPlanCancellationRequest("Farmer requested a different crop."), CancellationToken.None);

        var history = await db.CropPlanRequestHistories.SingleAsync();
        Assert.Equal(ApplicationRole.Admin, history.ChangedByRole);
        Assert.Equal("Farmer requested a different crop.", history.Reason);
        Assert.Equal(admin.Id, history.ChangedByUserId);
    }

    [Fact]
    public async Task Farmer_cannot_cancel_another_farmers_plan()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, CropPlanRequestStatus.Submitted);
        var otherFarmer = await AddUserAsync(db, ApplicationRole.Farmer, "other@example.test");

        var error = await Assert.ThrowsAsync<ApiException>(() => NewService(db, otherFarmer.Id, ApplicationRole.Farmer)
            .CancelCropPlanRequestAsync(data.Request.Id, new CropPlanCancellationRequest(null), CancellationToken.None));

        Assert.Equal("NOT_FOUND", error.Code);
        Assert.Equal(CropPlanRequestStatus.Submitted, data.Request.Status);
    }

    [Theory]
    [InlineData(CropPlanRequestStatus.Draft)]
    [InlineData(CropPlanRequestStatus.Submitted)]
    [InlineData(CropPlanRequestStatus.PreliminaryGenerated)]
    public async Task Every_active_non_terminal_request_status_can_be_cancelled(CropPlanRequestStatus status)
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, status);

        var response = await NewService(db, data.Farmer.Id, ApplicationRole.Farmer)
            .CancelCropPlanRequestAsync(data.Request.Id, new CropPlanCancellationRequest(null), CancellationToken.None);

        Assert.Equal(CropPlanRequestStatus.Cancelled, response.Status);
    }

    [Theory]
    [InlineData(CropPlanRequestStatus.Approved)]
    [InlineData(CropPlanRequestStatus.Rejected)]
    [InlineData(CropPlanRequestStatus.Cancelled)]
    public async Task Terminal_plan_cannot_be_cancelled(CropPlanRequestStatus status)
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, status);

        var error = await Assert.ThrowsAsync<ApiException>(() => NewService(db, data.Farmer.Id, ApplicationRole.Farmer)
            .CancelCropPlanRequestAsync(data.Request.Id, new CropPlanCancellationRequest(null), CancellationToken.None));

        Assert.Equal("CROP_PLAN_CANCELLATION_NOT_ALLOWED", error.Code);
    }

    [Fact]
    public async Task Archived_plan_cannot_be_cancelled_again()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, CropPlanRequestStatus.Cancelled);
        data.Request.IsDeleted = true;
        await db.SaveChangesAsync();
        var admin = await AddUserAsync(db, ApplicationRole.Admin, "archive-admin@example.test");

        var error = await Assert.ThrowsAsync<ApiException>(() => NewService(db, admin.Id, ApplicationRole.Admin)
            .CancelCropPlanRequestAsync(data.Request.Id, new CropPlanCancellationRequest("Retry"), CancellationToken.None));

        Assert.Equal("CROP_PLAN_ALREADY_ARCHIVED", error.Code);
    }

    [Theory]
    [InlineData(CropPlanRequestStatus.Cancelled)]
    [InlineData(CropPlanRequestStatus.Rejected)]
    public async Task Admin_can_soft_archive_terminal_plan_without_deleting_related_data(CropPlanRequestStatus status)
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, status);
        var admin = await AddUserAsync(db, ApplicationRole.Admin, $"archive-{status}@example.test");
        var workflowId = data.Workflow.Id;

        await NewService(db, admin.Id, ApplicationRole.Admin)
            .ArchiveCropPlanRequestAsync(data.Request.Id, CancellationToken.None);

        Assert.True(data.Request.IsDeleted);
        Assert.Equal(admin.Id, data.Request.UpdatedByUserId);
        Assert.True(await db.AgentWorkflows.AnyAsync(item => item.Id == workflowId));
        var history = await db.CropPlanRequestHistories.SingleAsync();
        Assert.Equal(CropPlanHistoryAction.Archived, history.Action);
        Assert.Equal(status, history.FromStatus);
        Assert.Equal(status, history.ToStatus);
        Assert.Equal(ApplicationRole.Admin, history.ChangedByRole);
    }

    [Theory]
    [InlineData(CropPlanRequestStatus.Draft)]
    [InlineData(CropPlanRequestStatus.Submitted)]
    [InlineData(CropPlanRequestStatus.PreliminaryGenerated)]
    [InlineData(CropPlanRequestStatus.Approved)]
    public async Task Admin_cannot_archive_non_removable_plan(CropPlanRequestStatus status)
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, status);
        var admin = await AddUserAsync(db, ApplicationRole.Admin, $"blocked-{status}@example.test");

        var error = await Assert.ThrowsAsync<ApiException>(() => NewService(db, admin.Id, ApplicationRole.Admin)
            .ArchiveCropPlanRequestAsync(data.Request.Id, CancellationToken.None));

        Assert.Equal("CROP_PLAN_ARCHIVE_NOT_ALLOWED", error.Code);
        Assert.False(data.Request.IsDeleted);
    }

    [Fact]
    public async Task Farmer_can_create_replacement_after_cancelling_active_plan()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, CropPlanRequestStatus.Submitted);
        var service = NewService(db, data.Farmer.Id, ApplicationRole.Farmer);
        await service.CancelCropPlanRequestAsync(data.Request.Id, new CropPlanCancellationRequest(null), CancellationToken.None);

        var replacement = await service.CreateCropPlanRequestAsync(new CropPlanRequestCreate(
            data.Request.FarmId,
            data.Field.Id,
            data.Request.CropTypeId,
            data.Request.PreferredStartDate,
            data.Request.PreferredEndDate,
            data.Request.Budget,
            "Replacement crop plan"), CancellationToken.None);

        Assert.Equal(CropPlanRequestStatus.Submitted, replacement.Status);
        Assert.NotEqual(data.Request.Id, replacement.Id);
    }

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CropPlanningService NewService(AppDbContext db, Guid userId, ApplicationRole role) =>
        new(
            db,
            new CurrentUserStub(role, userId),
            new FarmRequestValidator(),
            new FieldRequestValidator(),
            new CropTypeRequestValidator(),
            new CropCycleRequestValidator(),
            new CropPlanRequestCreateValidator(),
            new CropPlanRequestUpdateValidator());

    private static async Task<SeededPlan> SeedAsync(AppDbContext db, CropPlanRequestStatus status)
    {
        var farmer = new AppUser
        {
            FullName = "Farmer",
            Email = $"farmer-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.Farmer,
            IsActive = true
        };
        var officer = new AppUser
        {
            FullName = "Field Officer",
            Email = $"officer-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.FieldOfficer,
            IsActive = true
        };
        var farm = new Farm { Name = "North Farm", Location = "North", District = "Kurunegala", TotalArea = 10, OwnerUser = farmer };
        var field = new Field { Name = "Field A", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
        var crop = new CropType { Name = $"Rice {Guid.NewGuid():N}", IsActive = true };
        var request = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = crop,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2026, 10, 1),
            PreferredEndDate = new DateOnly(2027, 1, 1),
            Budget = 12000,
            Objective = "Plan safely",
            Status = status
        };
        var workflow = new AgentWorkflow
        {
            CropPlanRequest = request,
            InitiatedByUser = farmer,
            Objective = request.Objective,
            Status = AgentWorkflowStatus.Pending,
            CurrentStep = "CropFieldAnalysisAgent",
            Version = 3
        };
        db.AddRange(farmer, officer, request, workflow);
        await db.SaveChangesAsync();
        return new SeededPlan(farmer, officer, field, request, workflow);
    }

    private static async Task<AppUser> AddUserAsync(AppDbContext db, ApplicationRole role, string email)
    {
        var user = new AppUser { FullName = role.ToString(), Email = email, PasswordHash = "hash", Role = role, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed record SeededPlan(AppUser Farmer, AppUser Officer, Field Field, CropPlanRequest Request, AgentWorkflow Workflow);

    private sealed class CurrentUserStub(ApplicationRole role, Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => role == roleToCheck;
    }
}
