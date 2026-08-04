namespace Domain.Entities;

public sealed class ReportContractInformationRequest
{
    public Guid InformationRequestId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ReportContractId { get; set; }
    public Guid RequestedByAdminId { get; set; }
    public Guid TargetUserId { get; set; }
    public string Message { get; set; } = null!;
    public string? RequestedEvidenceOrClarification { get; set; }
    public DateTime? DueAt { get; set; }
    /// <summary>Enum ContractReportInformationRequestStatus: 0=Pending, 1=Responded, 2=Cancelled, 3=Expired.</summary>
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public ReportContract ReportContract { get; set; } = null!;
    public User RequestedByAdmin { get; set; } = null!;
    public User TargetUser { get; set; } = null!;
}
