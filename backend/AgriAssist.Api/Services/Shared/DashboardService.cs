using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.TaskApproval;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.Shared;

public sealed class DashboardService(AppDbContext dbContext) : IDashboardService
{
    public async Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var usersByRole = await dbContext.Users.AsNoTracking()
            .Where(user => !user.IsDeleted)
            .GroupBy(user => user.Role)
            .Select(group => new RoleCountResponse(group.Key.ToString(), group.Count()))
            .ToListAsync(cancellationToken);

        var activeFarms = await dbContext.Farms.AsNoTracking().CountAsync(farm => !farm.IsDeleted, cancellationToken);
        var activeCropPlans = await dbContext.CropPlanRequests.AsNoTracking().CountAsync(request =>
            !request.IsDeleted &&
            (request.Status == CropPlanRequestStatus.Submitted ||
             request.Status == CropPlanRequestStatus.PreliminaryGenerated ||
             request.Status == CropPlanRequestStatus.Approved),
            cancellationToken);
        var openCropIssues = await dbContext.CropIssues.AsNoTracking().CountAsync(issue =>
            !issue.IsDeleted && (issue.Status == CropIssueStatus.Open || issue.Status == CropIssueStatus.Escalated),
            cancellationToken);
        var lowStockResources = await dbContext.InventoryStocks.AsNoTracking().CountAsync(stock =>
            !stock.IsDeleted && stock.QuantityOnHand - stock.ReservedQuantity <= stock.LowStockThreshold,
            cancellationToken);
        var pendingTasks = await dbContext.FarmTasks.AsNoTracking().CountAsync(task =>
            !task.IsDeleted && task.Status == FarmTaskStatus.PendingApproval,
            cancellationToken);
        var pendingApprovals = await dbContext.IrrigationSchedules.AsNoTracking().CountAsync(schedule =>
            !schedule.IsDeleted && schedule.Status == IrrigationScheduleStatus.PendingApproval,
            cancellationToken);

        return new DashboardSummaryResponse(usersByRole, activeFarms, activeCropPlans, openCropIssues, lowStockResources, pendingTasks, pendingApprovals);
    }
}
