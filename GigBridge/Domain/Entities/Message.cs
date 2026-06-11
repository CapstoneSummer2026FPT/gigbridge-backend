using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Message
{
    public Guid MessagesId { get; set; }

    public Guid ConversationsId { get; set; }

    public Guid? SenderUserId { get; set; }

    /// <summary>
    /// Enum MessageType: 0=Text, 1=Image, 2=File, 3=System, 4=FinalOffer, 5=ContractEvent, 6=MilestoneEvent, 7=PaymentEvent, 8=DisputeEvent
    /// </summary>
    public int MessageType { get; set; }

    public string? Content { get; set; }

    public Guid? ReplyToMessageId { get; set; }

    public string? Metadata { get; set; }

    public string? ClientMessageId { get; set; }

    public DateTime SentAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public DateTime? DeletedForEveryoneAt { get; set; }

    public DateTime? DeletedForSenderAt { get; set; }

    public virtual Conversation Conversations { get; set; } = null!;

    public virtual ICollection<MessageAttachment> MessageAttachments { get; set; } = new List<MessageAttachment>();

    public virtual ICollection<Conversation> LastMessageForConversations { get; set; } = new List<Conversation>();

    public virtual ICollection<ConversationParticipant> LastReadByParticipants { get; set; } = new List<ConversationParticipant>();

    public virtual ICollection<Message> Replies { get; set; } = new List<Message>();

    public virtual Message? ReplyToMessage { get; set; }

    public virtual User? SenderUser { get; set; }
}
