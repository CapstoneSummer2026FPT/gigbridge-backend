namespace Domain.Entities;

public sealed class MilestoneEarlyStartRequest
{
    public Guid MilestoneEarlyStartRequestId { get; set; }
    public Guid ContractsId { get; set; }
    public Guid MilestonesId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? RespondedByUserId { get; set; }
    public string Reason { get; set; } = null!;
    public string? ResponseNote { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public Contract Contract { get; set; } = null!;
    public Milestone Milestone { get; set; } = null!;
}
