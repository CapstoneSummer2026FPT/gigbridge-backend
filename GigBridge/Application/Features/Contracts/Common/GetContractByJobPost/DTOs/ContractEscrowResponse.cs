namespace Application.Features.Contracts.Common.GetContractByJobPost.DTOs;

public class ContractEscrowResponse
{
    public Guid ContractEscrowId { get; set; }

    public decimal RequiredAmount { get; set; }

    public decimal RequiredTokens { get; set; }

    public decimal FundingFeeRate { get; set; }

    public decimal FundingFeeVnd { get; set; }

    public decimal FundingFeeTokens { get; set; }

    public decimal TotalDebitTokens { get; set; }

    public decimal FundedAmount { get; set; }

    public decimal ReleasedAmount { get; set; }

    public decimal RequiredPercentage { get; set; }

    public string Currency { get; set; } = string.Empty;

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? FundedAt { get; set; }
}
