using Application.Features.Admin.Dashboard.Dto;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}

