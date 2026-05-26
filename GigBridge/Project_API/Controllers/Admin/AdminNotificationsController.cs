using Application.Common.Models;
using Application.Features.Admin.Notifications.Command;
using Application.Features.Admin.Notifications.Dto;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Application.DTOs.Admin;

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

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] NotificationPageQueryDto query, CancellationToken cancellationToken)
    {
        var data = await _notificationService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Notifications retrieved successfully"));
    }

    [HttpGet("{notificationId:guid}")]
    public async Task<IActionResult> Get(Guid notificationId, CancellationToken cancellationToken)
    {
        var data = await _notificationService.GetAsync(notificationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Notification retrieved successfully"));
    }

    [HttpPost("system-alerts")]
    public async Task<IActionResult> SendSystemAlert([FromBody] SystemAlertRequestDto request, CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new SendSystemAlertCommand(request, GetActor()), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "System alert sent successfully"));
    }
}

