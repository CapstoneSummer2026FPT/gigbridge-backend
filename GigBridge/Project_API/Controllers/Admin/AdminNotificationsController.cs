using Application.DTOs.Admin;
using Application.Common.Models;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/notifications")]
public sealed class AdminNotificationsController : AdminControllerBase
{
    private readonly IAdminNotificationService _notificationService;

    public AdminNotificationsController(IAdminNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("system-alerts")]
    public async Task<IActionResult> SendSystemAlert([FromBody] SystemAlertRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _notificationService.SendSystemAlertAsync(request, GetActor(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "System alert sent successfully"));
    }
}
