using System;

namespace Domain.Entities;

public partial class NegotiationOffer
{
    public Guid NegotiationOfferId { get; set; }

    public Guid ConversationsId { get; set; }

    public Guid JobPostsId { get; set; }

    public Guid ContractsId { get; set; }

    public Guid? ProposalsId { get; set; }

    public Guid ClientProfilesId { get; set; }

    public Guid FreelancerProfilesId { get; set; }

    public decimal FinalPrice { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? ScopeSummary { get; set; }

    public string? ClientNote { get; set; }

    /// <summary>
    /// Enum NegotiationOfferStatus: 0=PendingFreelancerConfirmation, 1=Accepted, 2=Rejected, 3=ChangeRequested, 4=Expired, 5=Cancelled
    /// </summary>
    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public virtual ClientProfile ClientProfiles { get; set; } = null!;

    public virtual Contract Contracts { get; set; } = null!;

    public virtual Conversation Conversations { get; set; } = null!;

    public virtual FreelancerProfile FreelancerProfiles { get; set; } = null!;

    public virtual JobPost JobPosts { get; set; } = null!;

    public virtual Proposal? Proposals { get; set; }
}
