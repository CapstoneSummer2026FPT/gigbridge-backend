namespace Application.Features.Proposals.Common.DTOs;

public class ProposalWorkBreakdownItemDto
{
    public Guid? Id { get; set; }
    public Guid? MilestonePlanId { get; set; }
    public int? MilestoneOrderIndex { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Deliverables { get; set; }
    public string? EstimatedDuration { get; set; }
    public int OrderIndex { get; set; }
}

public class ProposalMilestonePlanDto
{
    public Guid? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? EstimatedDuration { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Deliverables { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public int OrderIndex { get; set; }
    public List<ProposalWorkBreakdownItemDto> WorkItems { get; set; } = [];
}
