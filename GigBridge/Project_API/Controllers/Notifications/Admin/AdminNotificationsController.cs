using Application.Common.Models;
using Application.Features.Admin.Notifications.Broadcast;
using Application.Features.Admin.Notifications.Common.DTOs;
using Application.Features.Admin.Notifications.Delete;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Notifications.Admin;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminNotificationsController : BaseApiController
{

    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] CreateAdminBroadcastRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
        {
            return InvalidTokenResponse();
        }

        var command = new CreateBroadcastCommand
        {
            CreatedByAdminId = adminId,
            Target = request.Target,
            TargetUserId = request.TargetUserId,
            Type = request.Type,
            Title = request.Title,
            Content = request.Content,
            ReferenceId = request.ReferenceId,
            ReferenceType = request.ReferenceType,
            ExpiresAt = request.ExpiresAt,
            SendEmail = request.SendEmail
        };

        await Mediator.Send(command);
        return Ok(ApiResponse<object>.Ok(null!, "Broadcast notification sent successfully."));
    }

    [HttpDelete("{notificationId:guid}")]
    public async Task<IActionResult> Delete(Guid notificationId)
    {
        var command = new DeleteAdminNotificationCommand
        {
            NotificationId = notificationId
        };

        await Mediator.Send(command);
        return Ok(ApiResponse<object>.Ok(null!, "Notification deleted successfully."));
    }
}
