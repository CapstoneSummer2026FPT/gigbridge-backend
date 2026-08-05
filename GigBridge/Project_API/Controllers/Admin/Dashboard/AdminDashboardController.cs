using Application.Common.Models;
using Application.Features.Admin.Dashboard.Common.DTOs;
using Application.Features.Admin.Dashboard.GetSummary.Queries;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.Dashboard;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminDashboardController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var summary = await Mediator.Send(new GetAdminDashboardSummaryQuery(days), cancellationToken);
        return Ok(ApiResponse<AdminDashboardSummary>.Ok(summary, "Admin dashboard summary loaded."));
    }
}
