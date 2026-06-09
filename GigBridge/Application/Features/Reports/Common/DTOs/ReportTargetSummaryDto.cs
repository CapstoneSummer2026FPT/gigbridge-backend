namespace Application.Features.Reports.Common.DTOs;

public class ReportTargetSummaryDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Email { get; set; }
    public int? Rating { get; set; }
}
