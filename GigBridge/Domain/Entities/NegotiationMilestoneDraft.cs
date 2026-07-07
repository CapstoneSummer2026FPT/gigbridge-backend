namespace Domain.Entities;

public class NegotiationMilestoneDraft
{
    public Guid NegotiationMilestoneDraftId { get; set; }
    public Guid ConversationsId { get; set; }
    public Guid? SourceProposalMilestonePlanId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? EstimatedDuration { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Deliverables { get; set; } = null!;
    public string AcceptanceCriteria { get; set; } = null!;
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Conversation Conversations { get; set; } = null!;
}
