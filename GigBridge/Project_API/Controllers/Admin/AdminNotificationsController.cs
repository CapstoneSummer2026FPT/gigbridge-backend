using Application.DTOs.Admin;
using Application.Common.Models;
using Application.Features.Admin.Notifications.SendSystemAlert;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/notifications")]
public sealed class AdminNotificationsController : AdminControllerBase
{
    [HttpPost("system-alerts")]
    public async Task<IActionResult> SendSystemAlert([FromBody] SystemAlertRequestDto request, CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new SendSystemAlertCommand(request, GetActor()), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "System alert sent successfully"));
    }
}
