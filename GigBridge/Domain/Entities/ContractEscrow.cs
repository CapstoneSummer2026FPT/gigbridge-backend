using Domain.Enums.Contracts.Escrow;
using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class ContractEscrow
{
    public Guid ContractEscrowId { get; set; }

    public Guid ContractsId { get; set; }

    public decimal RequiredAmount { get; set; }

    public decimal FundedAmount { get; set; }

    public decimal RequiredPercentage { get; set; } = 1.0m;

    public string Currency { get; set; } = "VND";

    public decimal ReleasedAmount { get; set; }

    /// <summary>
    /// Token portion of the held escrow funded from the client's deposited balance
    /// (non-withdrawable). Tracks how a refund must be restored across the client's
    /// two independent GigCoin pools. Maintained on fund / release / refund / penalty.
    /// </summary>
    public decimal DepositedTokens { get; set; }

    /// <summary>
    /// Token portion of the held escrow funded from the client's earned balance
    /// (withdrawable). See <see cref="DepositedTokens"/>.
    /// </summary>
    public decimal EarnedTokens { get; set; }

    /// <summary>
    /// Enum ContractEscrowStatus: 0=PendingFunding, 1=PartiallyFunded, 2=Funded, 3=PartiallyReleased, 4=Released, 5=Refunded, 6=Cancelled, 7=Disputed
    /// </summary>
    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? FundedAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public virtual Contract Contract { get; set; } = null!;

    public virtual ICollection<EscrowTransaction> EscrowTransactions { get; set; } = new List<EscrowTransaction>();

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
