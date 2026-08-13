namespace Domain.Entities;

public sealed class ProjectReceipt
{
    public Guid ProjectReceiptId { get; set; }
    public Guid ContractsId { get; set; }
    public Guid OwnerUserId { get; set; }
    public int ReceiptType { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public int TemplateVersion { get; set; } = 1;
    public DateTime IssuedAt { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string SnapshotHashSha256 { get; set; } = string.Empty;

    public int GenerationStatus { get; set; }
    public int GenerationAttemptCount { get; set; }
    public DateTime NextGenerationAttemptAt { get; set; }
    public Guid? GenerationLeaseToken { get; set; }
    public DateTime? GenerationLeaseExpiresAt { get; set; }
    public string? GenerationLastError { get; set; }

    public byte[]? PdfContent { get; set; }
    public string? PdfFileName { get; set; }
    public string? PdfContentType { get; set; }
    public long? PdfSizeBytes { get; set; }
    public string? PdfHashSha256 { get; set; }
    public DateTime? GeneratedAt { get; set; }

    public Guid? NotificationId { get; set; }
    public DateTime? NotifiedAt { get; set; }

    public int EmailStatus { get; set; }
    public int EmailAttemptCount { get; set; }
    public DateTime NextEmailAttemptAt { get; set; }
    public string? EmailLastError { get; set; }
    public DateTime? EmailedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Contract Contract { get; set; } = null!;
    public User OwnerUser { get; set; } = null!;
    public Notification? Notification { get; set; }
}
