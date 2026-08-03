namespace Domain.Entities;

public sealed class DisputePenalty
{
    public Guid DisputePenaltyId { get; set; }
    public Guid DisputeId { get; set; }
    public Guid ContractId { get; set; }
    public Guid MilestoneId { get; set; }
    public Guid? ViolatingUserId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = null!;
    public string? ResolutionNote { get; set; }
    public Guid CreatedByAdminId { get; set; }
    /// <summary>
    /// The client-side DisputePenalty wallet transaction that debits held escrow tokens.
    /// A penalty has no destination wallet transaction.
    /// </summary>
    public Guid? ClientDebitWalletTransactionId { get; set; }
    public Guid? EscrowTransactionId { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Dispute Dispute { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public Milestone Milestone { get; set; } = null!;
    public User? ViolatingUser { get; set; }
    public User CreatedByAdmin { get; set; } = null!;
    public WalletTransaction? ClientDebitWalletTransaction { get; set; }
    public EscrowTransaction? EscrowTransaction { get; set; }
}
