using Domain.Enums.Proposals;
namespace Application.Features.Proposals.Common.DTOs;

public class ProposalDetailDto
{
    public Guid ProposalId { get; set; }

    public Guid JobPostId { get; set; }

    public string? JobPostTitle { get; set; }

    public Guid FreelancerProfileId { get; set; }

    public string? FreelancerName { get; set; }

    public Guid? FreelancerUserId { get; set; }

    public string? CoverLetter { get; set; }

    public decimal? ProposedBudget { get; set; }

    public string? ProposedDuration { get; set; }

    public string? AnalysisSummary { get; set; }

    public string? SolutionApproach { get; set; }

    public string? Deliverables { get; set; }

    public string? Assumptions { get; set; }

    public string? OutOfScope { get; set; }

    public IReadOnlyCollection<ProposalWorkBreakdownItemDto> WorkBreakdownItems { get; set; } = [];

    public IReadOnlyCollection<ProposalMilestonePlanDto> MilestonePlans { get; set; } = [];

    /// <summary>
    /// Enum ProposalStatus: 0=Draft, 1=Pending, 2=Shortlisted, 3=Accepted, 4=Rejected, 5=Withdrawn
    /// </summary>
    public int Status { get; set; }

    public int ModerationStatus { get; set; }

    public string? InvalidationReason { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsAigenerated { get; set; }

    public bool HasAiInterview { get; set; }
    public bool AiInterviewCompleted { get; set; }
    public bool AiInterviewInProgress { get; set; }
    public Guid? AiInterviewDefinitionId { get; set; }
}
