using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Conversation
{
    public Guid ConversationsId { get; set; }

    /// <summary>
    /// Enum ConversationType: 0=JobNegotiation, 1=ContractWorkroom, 2=Dispute, 3=Support
    /// </summary>
    public int ConversationType { get; set; }

    public string? Title { get; set; }

    public Guid? JobPostsId { get; set; }

    public Guid? ProposalsId { get; set; }

    public Guid? ContractsId { get; set; }

    /// <summary>
    /// Optional dispute context for moderation/dispute conversations.
    /// </summary>
    public Guid? DisputesId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? LastMessageId { get; set; }

    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Enum ConversationStatus: 0=Active, 1=Archived, 2=Closed
    /// </summary>
    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Contract? Contracts { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual Dispute? Disputes { get; set; }

    public virtual JobPost? JobPosts { get; set; }

    public virtual Message? LastMessage { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<NegotiationOffer> NegotiationOffers { get; set; } = new List<NegotiationOffer>();

    public virtual ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

    public virtual Proposal? Proposals { get; set; }
}
