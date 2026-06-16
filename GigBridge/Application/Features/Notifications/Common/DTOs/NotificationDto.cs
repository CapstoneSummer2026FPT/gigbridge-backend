using Domain.Enums;

namespace Application.Features.Notifications.Common.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = "Personal";
    public Guid? NotificationId { get; set; }
    public Guid? BroadcastNotificationId { get; set; }
    public Guid? BroadcastRecipientId { get; set; }
    public Guid ReadTargetId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
