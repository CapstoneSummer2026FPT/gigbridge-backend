namespace Domain.Services.Payments;

public static class TokenWalletRules
{
    public const decimal VndPerToken = 1000m;

    public static decimal ToVnd(decimal tokens)
    {
        return decimal.Round(tokens * VndPerToken, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ToTokens(decimal vnd)
    {
        return decimal.Round(vnd / VndPerToken, 4, MidpointRounding.AwayFromZero);
    }

    public static decimal ToTokensCeiling(decimal vnd)
    {
        return Math.Ceiling(vnd / VndPerToken * 10000m) / 10000m;
    }
}
