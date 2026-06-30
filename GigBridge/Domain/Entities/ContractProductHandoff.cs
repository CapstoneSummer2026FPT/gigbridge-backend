namespace Domain.Entities;

public partial class ContractProductHandoff
{
    public Guid ContractProductHandoffId { get; set; }

    public Guid ContractsId { get; set; }

    public Guid SubmittedByUserId { get; set; }

    public int SourceType { get; set; }

    public string? FileName { get; set; }

    public string? FileUrl { get; set; }

    public string? MimeType { get; set; }

    public long? FileSizeBytes { get; set; }

    public string? ExternalUrl { get; set; }

    public string? Note { get; set; }

    public int Version { get; set; }

    public bool IsCurrent { get; set; }

    public Guid? ReceivedByUserId { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Contract Contract { get; set; } = null!;

    public virtual User SubmittedByUser { get; set; } = null!;

    public virtual User? ReceivedByUser { get; set; }
}
