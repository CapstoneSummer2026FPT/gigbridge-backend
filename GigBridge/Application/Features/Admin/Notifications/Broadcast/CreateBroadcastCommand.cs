using Application.Features.Admin.Notifications.Common.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Notifications.Broadcast;

public class CreateBroadcastCommand : IRequest
{
    public Guid? CreatedByAdminId { get; set; }
    public NotificationTarget Target { get; set; }
    public Guid? TargetUserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool SendEmail { get; set; }
}
