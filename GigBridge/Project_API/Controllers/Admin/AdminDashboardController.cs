using Application.Common.Models;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController : AdminControllerBase
{
    private readonly IAdminDashboardService _dashboardService;

    public AdminDashboardController(IAdminDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var data = await _dashboardService.GetSummaryAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Dashboard summary retrieved successfully"));
    }
}
