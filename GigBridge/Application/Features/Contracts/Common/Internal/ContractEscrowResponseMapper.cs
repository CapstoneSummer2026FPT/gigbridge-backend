using Application.Features.Contracts.Common.GetContractByJobPost.DTOs;
using Domain.Entities;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractEscrowResponseMapper
{
    public static ContractEscrowResponse ToResponse(ContractEscrow escrow)
    {
        var quote = ContractEscrowFundingQuote.Calculate(escrow.RequiredAmount);

        return new ContractEscrowResponse
        {
            ContractEscrowId = escrow.ContractEscrowId,
            RequiredAmount = escrow.RequiredAmount,
            RequiredTokens = quote.RequiredTokens,
            FundingFeeRate = quote.FundingFeeRate,
            FundingFeeVnd = quote.FundingFeeVnd,
            FundingFeeTokens = quote.FundingFeeTokens,
            TotalDebitTokens = quote.TotalDebitTokens,
            FundedAmount = escrow.FundedAmount,
            ReleasedAmount = escrow.ReleasedAmount,
            RequiredPercentage = escrow.RequiredPercentage,
            Currency = escrow.Currency,
            Status = escrow.Status,
            CreatedAt = escrow.CreatedAt,
            FundedAt = escrow.FundedAt
        };
    }
}
