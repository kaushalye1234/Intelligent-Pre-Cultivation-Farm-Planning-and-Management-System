$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$root = Join-Path $PSScriptRoot ".."

function Write-NoBom($Path, $Content) {
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

$resourceInterface = Join-Path $root "backend\AgriAssist.Api\Services\Resources\IResourceService.cs"
$content = [System.IO.File]::ReadAllText($resourceInterface)
$content = $content.Replace(
    "    Task<ResourceReservationResponse> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken);",
    "    Task<ResourceReservationResponse> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken);`r`n    Task<ResourceReservationResponse> CancelReservationAsync(Guid reservationId, CancellationToken cancellationToken);")
Write-NoBom $resourceInterface $content

$resourceController = Join-Path $root "backend\AgriAssist.Api\Controllers\Resources\ResourcesController.cs"
$content = [System.IO.File]::ReadAllText($resourceController)
$content = $content.Replace(
    '    [HttpPost("reservations/{id:guid}/release")]
    public async Task<ActionResult<ResourceReservationResponse>> Release(Guid id, CancellationToken cancellationToken) =>
        Ok(await resourceService.ReleaseAsync(id, cancellationToken));',
    '    [HttpPost("reservations/{id:guid}/release")]
    public async Task<ActionResult<ResourceReservationResponse>> Release(Guid id, CancellationToken cancellationToken) =>
        Ok(await resourceService.ReleaseAsync(id, cancellationToken));

    [HttpPost("reservations/{id:guid}/cancel")]
    public async Task<ActionResult<ResourceReservationResponse>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await resourceService.CancelReservationAsync(id, cancellationToken));')
Write-NoBom $resourceController $content

$resourceService = Join-Path $root "backend\AgriAssist.Api\Services\Resources\ResourceService.cs"
$content = [System.IO.File]::ReadAllText($resourceService)
$insert = @'

    public async Task<ResourceReservationResponse> CancelReservationAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var reservation = await dbContext.ResourceReservations.Include(item => item.InventoryStock).SingleOrDefaultAsync(item => item.Id == reservationId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Reservation");
        if (reservation.Status != ResourceReservationStatus.Active) throw new ApiException(HttpStatusCode.Conflict, "RESERVATION_NOT_ACTIVE", "Only active reservations can be cancelled.");
        var stock = reservation.InventoryStock ?? throw NotFound("Inventory stock");
        stock.ReservedQuantity -= reservation.Quantity;
        if (stock.ReservedQuantity < 0) throw new ApiException(HttpStatusCode.Conflict, "NEGATIVE_RESERVED_STOCK", "Reserved stock cannot be negative.");
        reservation.Status = ResourceReservationStatus.Cancelled;
        reservation.ReleasedAt = DateTime.UtcNow;
        dbContext.StockTransactions.Add(new StockTransaction { InventoryStockId = stock.Id, Type = StockTransactionType.Release, Quantity = reservation.Quantity, Note = "Reservation cancelled.", CreatedByUserId = currentUser.UserId });
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return MapReservation(reservation);
    }
'@
$content = $content.Replace("`r`n    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync", $insert + "`r`n    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync")
Write-NoBom $resourceService $content

$taskInterface = Join-Path $root "backend\AgriAssist.Api\Services\TaskApproval\ITaskApprovalService.cs"
$content = [System.IO.File]::ReadAllText($taskInterface)
$content = $content.Replace(
    "    Task<ApprovalDecisionResponse> ApproveScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);",
    "    Task<ApprovalDecisionResponse> ApproveScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);`r`n    Task<ApprovalDecisionResponse> RejectScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);`r`n    Task<ApprovalDecisionResponse> RequestScheduleRevisionAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken);")
Write-NoBom $taskInterface $content

$taskController = Join-Path $root "backend\AgriAssist.Api\Controllers\TaskApproval\TaskApprovalController.cs"
$content = [System.IO.File]::ReadAllText($taskController)
$content = $content.Replace(
    '    [HttpPost("schedules/{id:guid}/approve")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> ApproveSchedule(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.ApproveScheduleAsync(id, request, cancellationToken));',
    '    [HttpPost("schedules/{id:guid}/approve")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> ApproveSchedule(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.ApproveScheduleAsync(id, request, cancellationToken));

    [HttpPost("schedules/{id:guid}/reject")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> RejectSchedule(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.RejectScheduleAsync(id, request, cancellationToken));

    [HttpPost("schedules/{id:guid}/request-revision")]
    [Authorize(Roles = $"{nameof(ApplicationRole.AgriculturalOfficer)},{nameof(ApplicationRole.Admin)}")]
    public async Task<ActionResult<ApprovalDecisionResponse>> RequestScheduleRevision(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        Ok(await taskApprovalService.RequestScheduleRevisionAsync(id, request, cancellationToken));')
Write-NoBom $taskController $content

$taskService = Join-Path $root "backend\AgriAssist.Api\Services\TaskApproval\TaskApprovalService.cs"
$content = [System.IO.File]::ReadAllText($taskService)
$content = $content.Replace(
    '    public async Task<ApprovalDecisionResponse> ApproveScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        return await DecideScheduleAsync(scheduleId, request, ApprovalDecisionType.Approved, IrrigationScheduleStatus.Approved, cancellationToken);
    }',
    '    public Task<ApprovalDecisionResponse> ApproveScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        DecideScheduleAsync(scheduleId, request, ApprovalDecisionType.Approved, IrrigationScheduleStatus.Approved, cancellationToken);

    public Task<ApprovalDecisionResponse> RejectScheduleAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        DecideScheduleAsync(scheduleId, request, ApprovalDecisionType.Rejected, IrrigationScheduleStatus.Rejected, cancellationToken);

    public Task<ApprovalDecisionResponse> RequestScheduleRevisionAsync(Guid scheduleId, ApprovalActionRequest request, CancellationToken cancellationToken) =>
        DecideScheduleAsync(scheduleId, request, ApprovalDecisionType.RevisionRequested, IrrigationScheduleStatus.RevisionRequested, cancellationToken);')
$content = $content.Replace(
    '        var task = await dbContext.FarmTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Task");
        task.Status = nextStatus;',
    '        var task = await dbContext.FarmTasks.SingleOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Task");
        if (task.Status != FarmTaskStatus.PendingApproval) throw new ApiException(HttpStatusCode.Conflict, "TASK_DECISION_NOT_ALLOWED", "Only tasks pending approval can receive a decision.");
        task.Status = nextStatus;')
$content = $content.Replace(
    '        var schedule = await dbContext.IrrigationSchedules.SingleOrDefaultAsync(item => item.Id == scheduleId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Irrigation schedule");
        schedule.Status = nextStatus;',
    '        var schedule = await dbContext.IrrigationSchedules.SingleOrDefaultAsync(item => item.Id == scheduleId && !item.IsDeleted, cancellationToken) ?? throw NotFound("Irrigation schedule");
        if (schedule.Status != IrrigationScheduleStatus.PendingApproval) throw new ApiException(HttpStatusCode.Conflict, "SCHEDULE_DECISION_NOT_ALLOWED", "Only schedules pending approval can receive a decision.");
        schedule.Status = nextStatus;')
Write-NoBom $taskService $content

Write-NoBom (Join-Path $root "backend\AgriAssist.Api.Tests\AuditRegressionTests.cs") @'
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Services.TaskApproval;
using AgriAssist.Api.Validators.Resources;
using AgriAssist.Api.Validators.TaskApproval;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class AuditRegressionTests
{
    [Fact]
    public async Task Reservation_cancel_releases_stock_and_blocks_double_cancel()
    {
        await using var db = NewDbContext();
        var service = NewResourceService(db);
        var category = new ResourceCategory { Name = "Fertilizer" };
        var resource = new Resource { Name = "NPK", Unit = "kg", ResourceCategory = category, IsActive = true };
        var stock = new InventoryStock { Resource = resource, QuantityOnHand = 50, ReservedQuantity = 0, LowStockThreshold = 5 };
        db.InventoryStocks.Add(stock);
        await db.SaveChangesAsync();

        var reservation = await service.ReserveAsync(new ResourceReservationRequest(stock.Id, 20, "Audit reservation"), CancellationToken.None);
        var cancelled = await service.CancelReservationAsync(reservation.Id, CancellationToken.None);

        Assert.Equal(ResourceReservationStatus.Cancelled, cancelled.Status);
        Assert.Equal(0, (await db.InventoryStocks.SingleAsync()).ReservedQuantity);
        var exception = await Assert.ThrowsAsync<ApiException>(() => service.CancelReservationAsync(reservation.Id, CancellationToken.None));
        Assert.Equal("RESERVATION_NOT_ACTIVE", exception.Code);
    }

    [Fact]
    public async Task Task_approval_blocks_repeated_decision()
    {
        await using var db = NewDbContext();
        var service = NewTaskApprovalService(db, ApplicationRole.AgriculturalOfficer);
        var farmer = new AppUser { FullName = "Farmer", Email = "farmer.audit@example.test", PasswordHash = "hash", Role = ApplicationRole.Farmer, IsActive = true };
        var assignee = new AppUser { FullName = "Officer", Email = "officer.audit@example.test", PasswordHash = "hash", Role = ApplicationRole.FieldOfficer, IsActive = true };
        var farm = new Farm { Name = "Audit Farm", Location = "Audit", TotalArea = 10, OwnerUser = farmer };
        db.AddRange(farmer, assignee, farm);
        await db.SaveChangesAsync();

        var task = await service.CreateTaskAsync(new FarmTaskRequest(farm.Id, "Inspect", "Audit task", DateTime.UtcNow.AddDays(1), assignee.Id, FarmTaskStatus.PendingApproval), CancellationToken.None);
        await service.ApproveTaskAsync(task.Id, new ApprovalActionRequest("Approved", null), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.ApproveTaskAsync(task.Id, new ApprovalActionRequest("Approved again", null), CancellationToken.None));
        Assert.Equal("TASK_DECISION_NOT_ALLOWED", exception.Code);
    }

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ResourceService NewResourceService(AppDbContext db) =>
        new(
            db,
            new AuditCurrentUserService(ApplicationRole.ResourceOfficer),
            new ResourceCategoryRequestValidator(),
            new SupplierRequestValidator(),
            new ResourceRequestValidator(),
            new InventoryStockRequestValidator(),
            new ResourceReservationRequestValidator());

    private static TaskApprovalService NewTaskApprovalService(AppDbContext db, ApplicationRole role) =>
        new(
            db,
            new AuditCurrentUserService(role),
            new FarmTaskRequestValidator(),
            new IrrigationScheduleRequestValidator(),
            new ApprovalActionRequestValidator());

    private sealed class AuditCurrentUserService(ApplicationRole role) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }
}
'@
