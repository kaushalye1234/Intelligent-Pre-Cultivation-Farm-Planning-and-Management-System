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