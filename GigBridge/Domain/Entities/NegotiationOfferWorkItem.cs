namespace Domain.Entities;

public sealed class NegotiationOfferWorkItem
{
    public Guid NegotiationOfferWorkItemId { get; set; }
    public Guid NegotiationOfferMilestoneId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Deliverables { get; set; }
    public string? EstimatedDuration { get; set; }
    public int OrderIndex { get; set; }

    public NegotiationOfferMilestone Milestone { get; set; } = null!;
}
