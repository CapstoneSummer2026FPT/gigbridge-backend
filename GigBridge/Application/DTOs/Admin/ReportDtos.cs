namespace Application.DTOs.Admin;

public sealed class ReportPageQueryDto : PagedQueryDto
{
    public int? Type { get; set; }
}

public sealed class ReportDto
{
    public Guid ReportId { get; init; }
    public Guid ReporterId { get; init; }
    public Guid ReportedEntityId { get; init; }
    public string ReportedEntityType { get; init; } = string.Empty;
    public int Type { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int Status { get; init; }
    public string? AdminNote { get; init; }
    public Guid? ResolvedByAdminId { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? AdminAttachmentUrl { get; init; }
    public string? AdminAttachmentFileName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class ReportReviewRequestDto
{
    public string? AdminNote { get; set; }
}

public sealed class ReportResolutionRequestDto
{
    public int Status { get; set; }
    public string? AdminNote { get; set; }
    public string? AdminAttachmentUrl { get; set; }
    public string? AdminAttachmentFileName { get; set; }
}
