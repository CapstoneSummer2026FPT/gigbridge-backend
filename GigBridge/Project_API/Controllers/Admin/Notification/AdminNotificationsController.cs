using Application.Common.Models;
using Application.Features.Admin.Notifications.Broadcast;
using Application.Features.Admin.Notifications.Common.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.Notification;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminNotificationsController : BaseApiController
{

    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] CreateAdminBroadcastRequest request)
    {
        var command = new CreateBroadcastCommand
        {
            Target = request.Target,
            TargetUserId = request.TargetUserId,
            Type = request.Type,
            Title = request.Title,
            Content = request.Content,
            ReferenceId = request.ReferenceId,
            ReferenceType = request.ReferenceType,
            SendEmail = request.SendEmail
        };

        await Mediator.Send(command);
        return Ok(ApiResponse<object>.Ok(null!, "Broadcast notification sent successfully."));
    }
}
