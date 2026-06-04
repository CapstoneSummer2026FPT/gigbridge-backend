namespace Application.Features.Proposals.Common.DTOs;

public class ProposalDetailDto
{
    public Guid ProposalId { get; set; }

    public Guid JobPostId { get; set; }

    public string? JobPostTitle { get; set; }

    public Guid FreelancerProfileId { get; set; }

    public string? FreelancerName { get; set; }

    public string? CoverLetter { get; set; }

    public decimal? ProposedRate { get; set; }

    public string? ProposedDuration { get; set; }

    /// <summary>
    /// Enum ProposalStatus: 0=Pending, 1=Shortlisted, 2=Accepted, 3=Rejected, 4=Withdrawn
    /// </summary>
    public int Status { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsAigenerated { get; set; }
}