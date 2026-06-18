using System;

namespace Application.Features.Contracts.Common.GetMyContracts.DTOs;

public class ContractDtoResponse
{
    public Guid ContractsId { get; set; }
    public Guid JobPostsId { get; set; }
    public Guid ClientProfilesId { get; set; }
    public Guid? FreelancerProfilesId { get; set; }
    public Guid? ProposalsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalBudget { get; set; }
    public int Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? EsignContractPdfUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? FreelancerName { get; set; }
}
