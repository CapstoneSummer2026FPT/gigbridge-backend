using Application.Features.Contracts.ProductHandoffs.Common.DTOs;

namespace Application.Features.Contracts.Common.GetContractByJobPost.DTOs;

public class ContractDetailResponse
{
    public Guid ContractId { get; set; }

    public Guid JobPostId { get; set; }

    public Guid ClientProfileId { get; set; }

    public Guid? FreelancerProfileId { get; set; }

    public Guid? ProposalId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal TotalBudget { get; set; }

    public int Status { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool CanReview { get; set; }

    public bool HasReviewedByCurrentUser { get; set; }

    public ContractEscrowResponse? Escrow { get; set; }

    public string? JobTitle { get; set; }
    public string? JobDescription { get; set; }
    public string? ClientName { get; set; }
    public string? ClientEmail { get; set; }
    public string? FreelancerName { get; set; }
    public string? FreelancerEmail { get; set; }
    public Guid? ConversationId { get; set; }

    public ContractProductHandoffResponse? CurrentProductHandoff { get; set; }
}
