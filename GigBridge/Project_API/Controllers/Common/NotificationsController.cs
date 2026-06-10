using System.ComponentModel.DataAnnotations;
using Application.Common.Models;
using Application.Features.Notifications.Common.DTOs;
using Application.Features.Notifications.Public.MarkBroadcastAsRead.Command;
using Application.Features.Notifications.Public.MarkAllAsRead.Command;
using Application.Features.Notifications.Public.MarkAsRead.Command;
using Application.Features.Notifications.Queries.GetNotifications;
using Application.Features.Notifications.Queries.GetUnreadCount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[Authorize]
public class NotificationsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 20)] int pageSize = 20,
        [FromQuery] bool unreadOnly = false)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetNotificationsQuery
        {
            UserId = userId,
            PageNumber = page,
            PageSize = pageSize,
            UnreadOnly = unreadOnly
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PaginatedList<NotificationDto>>.Ok(result, "Notifications retrieved successfully."));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetUnreadCountQuery { UserId = userId };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<UnreadCountResponse>.Ok(result, "Unread count retrieved successfully."));
    }

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new MarkAsReadCommand
        {
            NotificationId = notificationId,
            UserId = userId
        };

        await Mediator.Send(command);
        return Ok(ApiResponse<object>.Ok(null!, "Notification marked as read."));
    }

    [HttpPut("broadcast-recipients/{recipientId:guid}/read")]
    public async Task<IActionResult> MarkBroadcastAsRead(Guid recipientId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new MarkBroadcastAsReadCommand
        {
            BroadcastRecipientId = recipientId,
            UserId = userId
        };

        await Mediator.Send(command);
        return Ok(ApiResponse<object>.Ok(null!, "Broadcast notification marked as read."));
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new MarkAllAsReadCommand { UserId = userId };
        await Mediator.Send(command);
        return Ok(ApiResponse<object>.Ok(null!, "All notifications marked as read."));
    }
}
