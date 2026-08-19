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

    public int GenerationStatus { get; set; }
    public int GenerationAttemptCount { get; set; }
    public DateTime NextGenerationAttemptAt { get; set; }
    public Guid? GenerationLeaseToken { get; set; }
    public DateTime? GenerationLeaseExpiresAt { get; set; }
    public string? GenerationLastError { get; set; }

    public long? PdfSizeBytes { get; set; }
    public DateTime? GeneratedAt { get; set; }

    public Guid? NotificationId { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public int NotificationAttemptCount { get; set; }
    public DateTime NextNotificationAttemptAt { get; set; }
    public string? NotificationLastError { get; set; }

    public DateTime? DeliveryLeaseExpiresAt { get; set; }

    public int EmailStatus { get; set; }
    public int EmailAttemptCount { get; set; }
    public DateTime NextEmailAttemptAt { get; set; }
    public string? EmailLastError { get; set; }
    public DateTime? EmailedAt { get; set; }

    /// <summary>
    /// Bumped every time the paired <see cref="ProjectReceiptContent"/> row changes, so callers can
    /// detect content changes without loading the heavy columns themselves.
    /// </summary>
    public int ContentRevision { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Contract Contract { get; set; } = null!;
    public User OwnerUser { get; set; } = null!;
    public Notification? Notification { get; set; }
    public ProjectReceiptContent? Content { get; set; }
}
