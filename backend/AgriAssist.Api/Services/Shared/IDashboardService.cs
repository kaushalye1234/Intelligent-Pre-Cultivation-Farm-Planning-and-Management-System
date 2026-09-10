using AgriAssist.Api.Dtos.Shared;

namespace AgriAssist.Api.Services.Shared;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);
}
