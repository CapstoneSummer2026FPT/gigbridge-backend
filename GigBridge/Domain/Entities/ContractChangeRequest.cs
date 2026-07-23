namespace Domain.Entities;

public sealed class ContractChangeRequest
{
    public Guid ContractChangeRequestId { get; set; }
    public Guid ContractsId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? RespondedByUserId { get; set; }
    public string Reason { get; set; } = null!;
    public string RequestedChanges { get; set; } = null!;
    public string? ResponseNote { get; set; }
    public string? ClarificationRequestNote { get; set; }
    public string? ClarificationResponseNote { get; set; }
    public Guid[] AffectedMilestoneIds { get; set; } = [];
    public Guid[] AffectedWorkItemIds { get; set; } = [];
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? ClarifiedAt { get; set; }

    public Contract Contract { get; set; } = null!;
    public ContractAmendment? Amendment { get; set; }
}
