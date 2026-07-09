using System;

namespace Domain.Entities;

public partial class WalletWithdrawal
{
    public Guid WalletWithdrawalId { get; set; }

    public Guid UserWalletsId { get; set; }

    public Guid UserId { get; set; }

    public Guid? BankAccountId { get; set; }

    public string BankCode { get; set; } = null!;

    public string BankName { get; set; } = null!;

    public string BankAccountNumberEncrypted { get; set; } = null!;

    public string BankAccountNumberMasked { get; set; } = null!;

    public string BankAccountName { get; set; } = null!;

    public decimal TokenAmount { get; set; }

    public decimal VndAmount { get; set; }

    public decimal FeeVnd { get; set; }

    public decimal NetVndAmount { get; set; }

    /// <summary>
    /// Enum WithdrawalStatus: 0=Pending, 1=Processing, 2=SyncRequired, 3=Success, 4=Failed, 5=Cancelled
    /// </summary>
    public int Status { get; set; }

    public string Provider { get; set; } = "PayOS";

    public string ProviderOrderCode { get; set; } = null!;

    public string? ProviderPayoutId { get; set; }

    public string? ProviderTransactionCode { get; set; }

    public string? ProviderRawStatus { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? FailureReason { get; set; }

    public string? LastSyncError { get; set; }

    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessingStartedAt { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual BankAccount? BankAccount { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual UserWallet UserWallet { get; set; } = null!;
}
