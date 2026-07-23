namespace Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;

public sealed class NegotiationWorkItemDto
{
    public Guid? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Deliverables { get; set; }
    public string? EstimatedDuration { get; set; }
    public int OrderIndex { get; set; }
}

public sealed class NegotiationMilestoneDto
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
    public IReadOnlyCollection<NegotiationWorkItemDto> WorkItems { get; set; } = [];
}

public sealed record UpdateNegotiationMilestonePlanRequest(
    IReadOnlyCollection<NegotiationMilestoneDto> Milestones);
