using System;

namespace Domain.Entities;

public partial class PayoutOutbox
{
    public Guid PayoutOutboxId { get; set; }

    public Guid WalletWithdrawalId { get; set; }

    public string PayoutKey { get; set; } = null!;

    /// <summary>
    /// Enum PayoutOutboxStatus: 0=Pending, 1=Processing, 2=Delivered, 3=DeadLettered, 4=Cancelled
    /// </summary>
    public int Status { get; set; }

    public int AttemptCount { get; set; }

    public DateTime NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public virtual WalletWithdrawal WalletWithdrawal { get; set; } = null!;
}
