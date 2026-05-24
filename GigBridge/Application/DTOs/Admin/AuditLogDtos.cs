namespace Application.DTOs.Admin;

public sealed class AuditLogPageQueryDto : PagedQueryDto
{
    public string? Action { get; set; }
    public string? EntityType { get; set; }
}

public sealed class AuditLogDto
{
    public Guid AuditLogId { get; init; }
    public Guid AdminId { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public string? EntityType { get; init; }
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime CreatedAt { get; init; }
}
