using Application.Features.Wallets.Common;
using Domain.Services.Payments;

namespace Application.Features.Contracts.Common.Internal;

internal sealed record ContractEscrowFundingQuote(
    decimal RequiredTokens,
    decimal FundingFeeRate,
    decimal FundingFeeVnd,
    decimal FundingFeeTokens,
    decimal TotalDebitTokens)
{
    public static ContractEscrowFundingQuote Calculate(decimal requiredAmountVnd)
    {
        var requiredTokens = TokenWalletRules.ToTokens(requiredAmountVnd);
        var fundingFeeVnd = ServiceFeeWorkflow.CalculateVnd(requiredAmountVnd);
        var fundingFeeTokens = TokenWalletRules.ToTokens(fundingFeeVnd);

        return new ContractEscrowFundingQuote(
            requiredTokens,
            ServiceFeeWorkflow.ServiceFeeRate,
            fundingFeeVnd,
            fundingFeeTokens,
            requiredTokens + fundingFeeTokens);
    }
}
