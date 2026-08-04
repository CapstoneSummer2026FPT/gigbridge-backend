namespace Domain.Entities;

public class NegotiationOfferMilestone
{
    public Guid NegotiationOfferMilestoneId { get; set; }
    public Guid NegotiationOfferId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? EstimatedDuration { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Deliverables { get; set; } = null!;
    public string AcceptanceCriteria { get; set; } = null!;
    public int OrderIndex { get; set; }

    public virtual NegotiationOffer NegotiationOffer { get; set; } = null!;
    public virtual ICollection<NegotiationOfferWorkItem> WorkItems { get; set; } = new List<NegotiationOfferWorkItem>();
}
