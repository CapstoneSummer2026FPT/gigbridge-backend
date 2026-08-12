using Domain.Enums.Reports;
using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class ReportContract
{
    public Guid ReportContractId { get; set; }

    public Guid ContractId { get; set; }

    public Guid ReporterId { get; set; }

    public Guid? RespondentId { get; set; }

    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// Enum ContractReportIssueType: 0=PaymentIssue, 1=MilestoneIssue, 2=Delay, 3=PoorQuality, 4=CommunicationProblem, 5=ScopeChange, 6=Other
    /// </summary>
    public int IssueType { get; set; }

    public string Description { get; set; } = null!;

    public string DesiredResolution { get; set; } = null!;

    /// <summary>
    /// Enum ContractReportStatus: 0=Pending, 1=WaitingReporterConfirmation, 2=Resolved, 3=Escalated
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Enum ContractReportResolutionAction: 0=AcceptIssue, 1=ProvideExplanation, 2=ProposeResolution, 3=RejectIssue
    /// </summary>
    public int? ResolutionAction { get; set; }

    public string? Explanation { get; set; }

    public string? ProposedResolution { get; set; }

    public string? RejectReason { get; set; }

    public Guid? ResolvedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? AssignedAdminId { get; set; }

    public DateTime? AssignedAt { get; set; }

    /// <summary>Enum ContractReportAdminStatus: 0=Open, 1=UnderReview, 2=AwaitingInformation, 3=Closed, 4=Dismissed, 5=Escalated, 6=LinkedToDispute.</summary>
    public int AdminReviewStatus { get; set; }

    public int? AdminResolutionAction { get; set; }

    public string? AdminResolutionNote { get; set; }

    public bool IsEscalatedToDispute { get; set; }

    public virtual Contract Contract { get; set; } = null!;

    public virtual User Reporter { get; set; } = null!;

    public virtual User? Respondent { get; set; }

    public virtual Milestone? Milestone { get; set; }

    public virtual User? ResolvedByUser { get; set; }

    public virtual User? AssignedAdmin { get; set; }

    public virtual ICollection<ReportContractAttachment> ReportContractAttachments { get; set; } = new List<ReportContractAttachment>();

    public virtual ICollection<ReportContractAdminNote> AdminNotes { get; set; } = new List<ReportContractAdminNote>();

    public virtual ICollection<ReportContractInformationRequest> InformationRequests { get; set; } = new List<ReportContractInformationRequest>();

    public virtual ICollection<Dispute> RelatedDisputes { get; set; } = new List<Dispute>();
}
