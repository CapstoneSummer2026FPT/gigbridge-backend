using System;

namespace Application.Features.Proposals.Common.DTOs;

public class ProposalDto
{
    public Guid ProposalsId { get; set; }
    public Guid JobPostsId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid FreelancerProfilesId { get; set; }
    public string FreelancerName { get; set; } = string.Empty;
    public string CoverLetter { get; set; } = string.Empty;
    public decimal ProposedBudget { get; set; }
    public string ProposedDuration { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
