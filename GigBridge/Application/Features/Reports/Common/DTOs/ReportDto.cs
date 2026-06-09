using Domain.Enums;

namespace Application.Features.Reports.Common.DTOs;

public class ReportDto
{
    public Guid Id { get; set; }
    public ReportUserSummaryDto Reporter { get; set; } = new();
    public Guid ReportedEntityId { get; set; }
    public string ReportedEntityType { get; set; } = string.Empty;
    public ReportType Type { get; set; }
    public ReportStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public ReportUserSummaryDto? ResolvedByAdmin { get; set; }
    public ReportTargetSummaryDto? TargetSummary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
