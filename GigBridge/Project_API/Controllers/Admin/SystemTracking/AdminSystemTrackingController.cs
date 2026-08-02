using Application.Common.Models;
using Application.Features.Admin.SystemTracking.Common.DTOs;
using Application.Features.Admin.SystemTracking.GetSnapshot.Queries;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.SystemTracking;

[ApiController]
[Route("api/admin/system-tracking")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminSystemTrackingController : BaseApiController
{
    private readonly IHostEnvironment _environment;

    public AdminSystemTrackingController(IHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetSnapshot([FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        var snapshot = await Mediator.Send(
            new GetSystemTrackingSnapshotQuery(_environment.EnvironmentName, limit), cancellationToken);
        return Ok(ApiResponse<SystemTrackingSnapshot>.Ok(snapshot, "System tracking snapshot retrieved"));
    }
}
