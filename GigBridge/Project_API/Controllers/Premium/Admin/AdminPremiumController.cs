using Application.Common.Models;
using Application.Features.Admin.Premium;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Premium.Admin;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/premium")]
public sealed class AdminPremiumController : BaseApiController
{
    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetUser(Guid userId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetPremiumUserDiagnosticsQuery(userId), ct);
        return Ok(ApiResponse<PremiumUserDiagnosticsDto>.Ok(result, "Success"));
    }
}
