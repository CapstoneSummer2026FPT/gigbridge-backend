using Domain.Enums.JobInvitations;
namespace Domain.Entities;

public partial class JobInvitation
{
    public Guid JobInvitationsId { get; set; }

    public Guid JobPostsId { get; set; }
    public Guid ClientProfilesId { get; set; }
    public Guid FreelancerProfilesId { get; set; }

    public Guid? ProposalsId { get; set; }

    /// <summary>
    /// Enum JobInvitationStatus:
    /// 0 = Pending,
    /// 1 = Viewed,
    /// 2 = Applied,
    /// 3 = Declined,
    /// 4 = Expired,
    /// 5 = Cancelled
    /// </summary>
    public int Status { get; set; }

    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ViewedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public string? DeclineReason { get; set; }

    public virtual JobPost JobPosts { get; set; } = null!;
    public virtual ClientProfile ClientProfiles { get; set; } = null!;
    public virtual FreelancerProfile FreelancerProfiles { get; set; } = null!;
    public virtual Proposal? Proposals { get; set; }
}