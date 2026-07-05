namespace Domain.Entities;

public class ProposalMilestonePlan
{
    public Guid ProposalMilestonePlansId { get; set; }
    public Guid ProposalsId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? Deliverables { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public int OrderIndex { get; set; }

    public virtual Proposal Proposals { get; set; } = null!;
}
