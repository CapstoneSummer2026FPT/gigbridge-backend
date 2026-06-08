using Application.Features.Admin.Notifications.Common.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Notifications.Broadcast;

public class CreateBroadcastCommand : IRequest
{
    public NotificationTarget Target { get; set; }
    public Guid? TargetUserId { get; set; }
    public int Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public bool SendEmail { get; set; }
}
