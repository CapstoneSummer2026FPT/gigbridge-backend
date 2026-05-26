using Application.Features.Admin.Dashboard.Dto;
using Application.Features.Admin.Dashboard.Query;
using Infrastructure.Services.Admin.Interfaces;
using MediatR;

namespace Infrastructure.Services.Admin.Handlers.Dashboard;

public sealed class GetAdminDashboardHandler : IRequestHandler<GetAdminDashboardQuery, DashboardSummaryDto>
{
    private readonly IAdminDashboardService _dashboardService;

    public GetAdminDashboardHandler(IAdminDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public Task<DashboardSummaryDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken) =>
        _dashboardService.GetSummaryAsync(cancellationToken);
}
