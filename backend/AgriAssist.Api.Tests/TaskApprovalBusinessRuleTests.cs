using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Services.TaskApproval;
using AgriAssist.Api.Validators.TaskApproval;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class TaskApprovalBusinessRuleTests
{
    [Fact]
    public async Task Update_cannot_bypass_task_approval_transition()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, ApplicationRole.FieldOfficer, data.Officer.Id);
        var request = new FarmTaskRequest(data.Farm.Id, "Inspect field", "Check crop condition", DateTime.UtcNow.AddDays(2), data.Officer.Id, FarmTaskStatus.PendingApproval);
        var task = await service.CreateTaskAsync(request, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.UpdateTaskAsync(
            task.Id,
            request with { Status = FarmTaskStatus.Approved },
            CancellationToken.None));

        Assert.Equal("TASK_STATUS_CHANGE_NOT_ALLOWED", exception.Code);
        Assert.Equal(FarmTaskStatus.PendingApproval, (await db.FarmTasks.SingleAsync()).Status);
    }

    [Fact]
    public async Task Reject_requires_an_officer_written_comment()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var creator = NewService(db, ApplicationRole.FieldOfficer, data.Officer.Id);
        var task = await creator.CreateTaskAsync(
            new FarmTaskRequest(data.Farm.Id, "Inspect field", "Check crop condition", DateTime.UtcNow.AddDays(2), data.Officer.Id, FarmTaskStatus.PendingApproval),
            CancellationToken.None);
        var approver = NewService(db, ApplicationRole.AgriculturalOfficer, data.Approver.Id);

        var exception = await Assert.ThrowsAsync<ApiException>(() => approver.RejectTaskAsync(
            task.Id,
            new ApprovalActionRequest("   ", null),
            CancellationToken.None));

        Assert.Equal("DECISION_COMMENT_REQUIRED", exception.Code);
        Assert.Empty(db.ApprovalDecisions);
    }

    [Fact]
    public async Task Schedule_create_rejects_overlap_for_the_same_field()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, ApplicationRole.FieldOfficer, data.Officer.Id);
        var start = DateTime.UtcNow.AddDays(2);
        await service.CreateScheduleAsync(
            new IrrigationScheduleRequest(data.Field.Id, start, 90, "Morning irrigation", IrrigationScheduleStatus.PendingApproval),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.CreateScheduleAsync(
            new IrrigationScheduleRequest(data.Field.Id, start.AddMinutes(30), 30, "Overlapping irrigation", IrrigationScheduleStatus.PendingApproval),
            CancellationToken.None));

        Assert.Equal("IRRIGATION_SCHEDULE_CONFLICT", exception.Code);
    }

    [Fact]
    public async Task Farmer_approval_history_is_scoped_to_owned_farms()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var otherFarmer = new AppUser { FullName = "Other Farmer", Email = "other.farmer@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var otherFarm = new Farm { Name = "Other Farm", Location = "South", TotalArea = 5, OwnerUser = otherFarmer };
        db.AddRange(otherFarmer, otherFarm);
        await db.SaveChangesAsync();

        var creator = NewService(db, ApplicationRole.FieldOfficer, data.Officer.Id);
        var first = await creator.CreateTaskAsync(
            new FarmTaskRequest(data.Farm.Id, "Owned task", "Visible", DateTime.UtcNow.AddDays(2), data.Officer.Id, FarmTaskStatus.PendingApproval),
            CancellationToken.None);
        var second = await creator.CreateTaskAsync(
            new FarmTaskRequest(otherFarm.Id, "Other task", "Hidden", DateTime.UtcNow.AddDays(3), data.Officer.Id, FarmTaskStatus.PendingApproval),
            CancellationToken.None);
        var approver = NewService(db, ApplicationRole.AgriculturalOfficer, data.Approver.Id);
        await approver.ApproveTaskAsync(first.Id, new ApprovalActionRequest("Approved", null), CancellationToken.None);
        await approver.ApproveTaskAsync(second.Id, new ApprovalActionRequest("Approved", null), CancellationToken.None);

        var farmerService = NewService(db, ApplicationRole.Farmer, data.Farmer.Id);
        var history = await farmerService.SearchApprovalsAsync(new ApprovalHistoryQuery(), CancellationToken.None);

        var decision = Assert.Single(history.Items);
        Assert.Equal(first.Id, decision.FarmTaskId);
    }

    [Fact]
    public async Task Cancellation_records_a_decision_reason()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var service = NewService(db, ApplicationRole.FieldOfficer, data.Officer.Id);
        var task = await service.CreateTaskAsync(
            new FarmTaskRequest(data.Farm.Id, "Inspect field", "Check crop condition", DateTime.UtcNow.AddDays(2), data.Officer.Id, FarmTaskStatus.PendingApproval),
            CancellationToken.None);

        var cancelled = await service.CancelTaskAsync(task.Id, new CancellationRequest("No longer required"), CancellationToken.None);

        Assert.Equal(FarmTaskStatus.Cancelled, cancelled.Status);
        var decision = Assert.Single(db.ApprovalDecisions);
        Assert.Equal(ApprovalDecisionType.Cancelled, decision.Decision);
        Assert.Equal("No longer required", decision.Comment);
    }

    [Fact]
    public async Task Item_endpoint_cannot_bypass_workflow_generated_task_gate()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db);
        var workflow = new AgentWorkflow
        {
            InitiatedByUserId = data.Farmer.Id,
            Objective = "Test workflow",
            Status = AgentWorkflowStatus.PendingOfficerApproval,
            CurrentStep = "HumanApproval"
        };
        var task = new FarmTask
        {
            FarmId = data.Farm.Id,
            AssignedToUserId = data.Officer.Id,
            Title = "Generated task",
            DueAt = DateTime.UtcNow.AddDays(2),
            Status = FarmTaskStatus.PendingApproval,
            GeneratedByWorkflow = workflow,
            CandidateRevision = 1
        };
        db.AddRange(workflow, task);
        await db.SaveChangesAsync();
        var approver = NewService(db, ApplicationRole.AgriculturalOfficer, data.Approver.Id);

        var exception = await Assert.ThrowsAsync<ApiException>(() => approver.ApproveTaskAsync(
            task.Id,
            new ApprovalActionRequest("Approve directly", workflow.Id),
            CancellationToken.None));

        Assert.Equal("WORKFLOW_ITEM_DECISION_REQUIRED", exception.Code);
        Assert.Empty(db.ApprovalDecisions);
    }

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static TaskApprovalService NewService(AppDbContext db, ApplicationRole role, Guid userId) =>
        new(
            db,
            new FixedCurrentUserService(role, userId),
            new FarmTaskRequestValidator(),
            new IrrigationScheduleRequestValidator(),
            new ApprovalActionRequestValidator(),
            new CancellationRequestValidator());

    private static async Task<SeededData> SeedAsync(AppDbContext db)
    {
        var farmer = new AppUser { FullName = "Farmer", Email = "farmer@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var officer = new AppUser { FullName = "Field Officer", Email = "field@example.test", PasswordHash = "hash", Role = ApplicationRole.FieldOfficer, IsActive = true };
        var approver = new AppUser { FullName = "Agricultural Officer", Email = "agri@example.test", PasswordHash = "hash", Role = ApplicationRole.AgriculturalOfficer, IsActive = true };
        var farm = new Farm { Name = "Test Farm", Location = "North", TotalArea = 10, OwnerUser = farmer };
        var field = new Field { Name = "Field A", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
        db.AddRange(farmer, officer, approver, farm, field);
        await db.SaveChangesAsync();
        return new SeededData(farmer, officer, approver, farm, field);
    }

    private sealed record SeededData(AppUser Farmer, AppUser Officer, AppUser Approver, Farm Farm, Field Field);

    private sealed class FixedCurrentUserService(ApplicationRole role, Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }
}
