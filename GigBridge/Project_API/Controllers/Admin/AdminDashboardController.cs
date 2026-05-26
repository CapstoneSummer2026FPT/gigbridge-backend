using Application.Common.Models;
using Application.Features.Admin.Dashboard.Query;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController : AdminControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new GetAdminDashboardQuery(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Dashboard summary retrieved successfully"));
    }
}
