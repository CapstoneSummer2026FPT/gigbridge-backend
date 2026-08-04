using System;
using Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;

namespace Application.Features.Contracts.Freelancer.GetMyCompletedProjects.DTOs;

public sealed class FreelancerCompletedProjectResponse
{
    public Guid ContractId { get; set; }

    public Guid JobPostsId { get; set; }

    public decimal TotalBudget { get; set; }

    public int Status { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public bool CanReview { get; set; }

    public bool HasReviewedByCurrentUser { get; set; }

    /// <summary>
    /// Full job post information for the completed project. Kept out of the
    /// contract DTOs on purpose — the contract should not expose every job post field.
    /// </summary>
    public JobPostDetailDto JobPost { get; set; } = null!;
}
