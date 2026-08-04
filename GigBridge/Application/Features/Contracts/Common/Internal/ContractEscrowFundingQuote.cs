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
    public static ContractEscrowFundingQuote Calculate(decimal requiredAmount)
    {
        // Contract/escrow amounts are G-coin: required tokens equal the required
        // amount directly (no VND -> token division). The 1% fee is charged on the
        // G-coin amount; its VND value is kept only as a reporting companion.
        var requiredTokens = requiredAmount;
        var fundingFeeTokens = decimal.Round(
            requiredAmount * ServiceFeeWorkflow.ServiceFeeRate,
            4,
            MidpointRounding.AwayFromZero);
        var fundingFeeVnd = TokenWalletRules.ToVnd(fundingFeeTokens);

        return new ContractEscrowFundingQuote(
            requiredTokens,
            ServiceFeeWorkflow.ServiceFeeRate,
            fundingFeeVnd,
            fundingFeeTokens,
            requiredTokens + fundingFeeTokens);
    }
}
