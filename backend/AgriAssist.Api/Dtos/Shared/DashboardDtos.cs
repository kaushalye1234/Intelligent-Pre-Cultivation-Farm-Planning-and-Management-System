namespace AgriAssist.Api.Dtos.Shared;

public sealed record RoleCountResponse(string Role, int Count);

public sealed record DashboardSummaryResponse(
    IReadOnlyList<RoleCountResponse> UsersByRole,
    int ActiveFarms,
    int ActiveCropPlans,
    int OpenCropIssues,
    int LowStockResources,
    int PendingTasks,
    int PendingApprovals);
