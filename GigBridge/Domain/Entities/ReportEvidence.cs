namespace Domain.Entities;

public sealed class ReportEvidence
{
    public Guid ReportEvidenceId { get; set; }
    public Guid ReportId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string StorageKey { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public Report Report { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
