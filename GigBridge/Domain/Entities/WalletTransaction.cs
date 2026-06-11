using System;

namespace Domain.Entities;

public partial class WalletTransaction
{
    public Guid WalletTransactionsId { get; set; }

    public Guid UserWalletsId { get; set; }

    public Guid UserId { get; set; }

    public Guid? ContractsId { get; set; }

    public Guid? ContractEscrowId { get; set; }

    public Guid? MilestonesId { get; set; }

    public decimal TokenAmount { get; set; }

    public decimal VndAmount { get; set; }

    /// <summary>
    /// Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Enum WalletTransactionStatus: 0=Pending, 1=Succeeded, 2=Failed, 3=Cancelled
    /// </summary>
    public int Status { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? GatewayProvider { get; set; }

    public string? GatewayOrderCode { get; set; }

    public string? GatewayTransactionCode { get; set; }

    public string? Metadata { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual Contract? Contract { get; set; }

    public virtual ContractEscrow? ContractEscrow { get; set; }

    public virtual Milestone? Milestone { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual UserWallet UserWallet { get; set; } = null!;
}
