using System;

namespace Domain.Entities;

public partial class EscrowTransaction
{
    public Guid EscrowTransactionId { get; set; }

    public Guid ContractEscrowId { get; set; }

    public Guid? MilestonesId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// Enum EscrowTransactionType: 0=Deposit, 1=ReleaseToFreelancer, 2=RefundToClient, 3=PlatformFee, 4=Adjustment
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Enum EscrowTransactionStatus: 0=Pending, 1=Succeeded, 2=Failed, 3=Cancelled
    /// </summary>
    public int Status { get; set; }

    public string? PaymentGateway { get; set; }

    public string? GatewayTransactionCode { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ContractEscrow ContractEscrow { get; set; } = null!;

    public virtual Milestone? Milestone { get; set; }
}
