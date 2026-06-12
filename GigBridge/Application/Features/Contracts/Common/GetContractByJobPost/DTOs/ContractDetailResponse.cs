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

    public string? DisputeTerms { get; set; }

    public int Status { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ContractEscrowResponse? Escrow { get; set; }
}
