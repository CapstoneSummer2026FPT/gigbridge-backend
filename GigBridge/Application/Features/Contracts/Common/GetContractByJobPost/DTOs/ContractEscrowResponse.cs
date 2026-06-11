namespace Application.Features.Contracts.Common.GetContractByJobPost.DTOs;

public class ContractEscrowResponse
{
    public Guid ContractEscrowId { get; set; }

    public decimal RequiredAmount { get; set; }

    public decimal FundedAmount { get; set; }

    public decimal RequiredPercentage { get; set; }

    public string Currency { get; set; } = string.Empty;

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? FundedAt { get; set; }
}
