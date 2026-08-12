using Domain.Enums.Wallets;

namespace Domain.Entities;

public sealed class PlatformRevenueEvent
{
    public Guid PlatformRevenueEventId { get; set; }
    public PlatformRevenueSource Source { get; set; }
    public Guid? WalletTransactionId { get; set; }
    public Guid? WalletWithdrawalId { get; set; }
    public Guid? PayerUserId { get; set; }
    public Guid? ContractId { get; set; }
    public string SourceEntityType { get; set; } = string.Empty;
    public Guid? SourceEntityId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public decimal GigCoinAmount { get; set; }
    public decimal VndEquivalent { get; set; }
    public decimal VndPerGigCoin { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool IsBackfilled { get; set; }
    public string? Metadata { get; set; }

    public WalletTransaction? WalletTransaction { get; set; }
    public WalletWithdrawal? WalletWithdrawal { get; set; }
    public User? PayerUser { get; set; }
    public Contract? Contract { get; set; }
}
