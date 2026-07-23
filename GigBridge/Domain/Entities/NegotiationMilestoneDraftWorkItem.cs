namespace Domain.Entities;

public sealed class NegotiationMilestoneDraftWorkItem
{
    public Guid NegotiationMilestoneDraftWorkItemId { get; set; }
    public Guid NegotiationMilestoneDraftId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Deliverables { get; set; }
    public string? EstimatedDuration { get; set; }
    public int OrderIndex { get; set; }

    public NegotiationMilestoneDraft MilestoneDraft { get; set; } = null!;
}
