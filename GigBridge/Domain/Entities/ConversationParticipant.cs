using System;

namespace Domain.Entities;

public partial class ConversationParticipant
{
    public Guid ConversationParticipantId { get; set; }

    public Guid ConversationsId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Enum ParticipantRole: 0=Client, 1=Freelancer, 2=Admin, 3=Support
    /// </summary>
    public int ParticipantRole { get; set; }

    public DateTime JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }

    public Guid? LastReadMessageId { get; set; }

    public DateTime? LastReadAt { get; set; }

    public int UnreadCount { get; set; }

    public bool IsMuted { get; set; }

    public bool IsPinned { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Conversation Conversations { get; set; } = null!;

    public virtual Message? LastReadMessage { get; set; }

    public virtual User User { get; set; } = null!;
}
