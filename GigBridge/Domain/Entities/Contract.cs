namespace Domain.Entities;

public partial class Contract
{
    public Guid ContractsId { get; set; }

    public Guid JobPostsId { get; set; }

    public Guid ClientProfilesId { get; set; }

    public Guid? FreelancerProfilesId { get; set; }

    public Guid? ProposalsId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Enum ContractStatus: 0=Draft, 1=PendingFreelancerSelection, 2=InNegotiation, 3=PendingContractDetails, 4=PendingContractConfirmation, 5=PendingEscrow, 6=PendingSignature, 7=Active, 8=Completed, 9=Cancelled, 10=Disputed
    /// </summary>
    public int Status { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// v1.2: URL bản hợp đồng lao động e-sign PDF khi có tranh chấp thanh toán
    /// </summary>
    public string? EsignContractPdfUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ClientProfile ClientProfiles { get; set; } = null!;

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();

    public virtual ContractEscrow? ContractEscrow { get; set; }

    public virtual EsignDocument? EsignDocument { get; set; }

    public virtual FreelancerProfile? FreelancerProfiles { get; set; }

    public virtual JobPost JobPosts { get; set; } = null!;

    public virtual ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();

    public virtual ICollection<NegotiationOffer> NegotiationOffers { get; set; } = new List<NegotiationOffer>();

    public virtual Proposal? Proposals { get; set; }

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
