using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Dispute
{
    public Guid DisputesId { get; set; }

    public Guid ContractsId { get; set; }

    public Guid InitiatorId { get; set; }

    public Guid? MilestonesId { get; set; }

    public string Reason { get; set; } = null!;

    /// <summary>
    /// Enum DisputeStatus: 0=Open, 1=UnderReview, 2=Resolved, 3=Closed
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Enum DisputeResolution: 0=ClientFavored, 1=FreelancerFavored, 2=Split, 3=Dismissed
    /// </summary>
    public int? Resolution { get; set; }

    public string? ResolutionNote { get; set; }

    public Guid? ResolvedByAdminId { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Contract Contracts { get; set; } = null!;

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<DisputeEvidence> DisputeEvidences { get; set; } = new List<DisputeEvidence>();

    public virtual ICollection<DisputeMessage> DisputeMessages { get; set; } = new List<DisputeMessage>();

    public virtual Milestone? Milestones { get; set; }

    public virtual User? ResolvedByAdmin { get; set; }

    public virtual User Initiator { get; set; } = null!;
}
