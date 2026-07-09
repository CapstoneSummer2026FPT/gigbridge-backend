using System;

namespace Domain.Entities;

public partial class PayoutWebhookLog
{
    public Guid PayoutWebhookLogId { get; set; }

    public string Provider { get; set; } = null!;

    public string? EventId { get; set; }

    public string? SignatureHash { get; set; }

    public Guid? WalletWithdrawalId { get; set; }

    public string RawPayload { get; set; } = null!;

    /// <summary>
    /// Enum PayoutWebhookProcessingStatus: 0=Pending, 1=Processed, 2=Rejected, 3=Failed
    /// </summary>
    public int ProcessingStatus { get; set; }

    public string? Error { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public virtual WalletWithdrawal? WalletWithdrawal { get; set; }
}
