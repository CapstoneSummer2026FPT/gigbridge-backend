using Application.DTOs.Admin;

namespace Application.Features.Admin.Notifications.Dto;

public sealed class NotificationPageQueryDto : PagedQueryDto
{
    public int? Type { get; set; }
    public Guid? UserId { get; set; }
    public bool? IsRead { get; set; }
    public string? ReferenceType { get; set; }
}

public sealed class AdminNotificationDto
{
    public Guid NotificationId { get; init; }
    public Guid UserId { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientEmail { get; init; }
    public int Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Content { get; init; }
    public Guid? ReferenceId { get; init; }
    public string? ReferenceType { get; init; }
    public bool? IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class SystemAlertRequestDto
{
    public List<Guid> UserIds { get; set; } = [];
    public string? Title { get; set; }
    public string? Content { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
}

public sealed class SystemAlertResultDto
{
    public int RecipientCount { get; init; }
}
