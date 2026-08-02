using System;

namespace Domain.Services;

/// <summary>
/// Pure GigCoin spending rules. The wallet keeps two independent spendable pools:
/// deposited tokens (non-withdrawable, spendable) and earned tokens (spendable and
/// withdrawable). Every spend must draw from the deposited balance first, then from
/// the earned balance. This is the single source of truth for that split so the
/// rule is never duplicated across handlers or controllers.
/// </summary>
public readonly record struct BalanceUsage(decimal DepositedAmount, decimal EarnedAmount);

public static class WalletSpendingService
{
    /// <summary>
    /// Splits a requested spend across the deposited and earned balances using the
    /// spend-deposited-first rule:
    /// <code>
    /// depositedUsed = min(depositedTokens, requestedAmount)
    /// earnedUsed    = requestedAmount - depositedUsed
    /// </code>
    /// Returns <see langword="null"/> when the combined balances cannot cover the
    /// request (or the request is non-positive).
    /// </summary>
    public static BalanceUsage? CalculateBalanceUsage(
        decimal depositedTokens,
        decimal earnedTokens,
        decimal requestedAmount)
    {
        if (requestedAmount <= 0m)
        {
            return null;
        }

        var depositedUsed = Math.Min(depositedTokens, requestedAmount);
        var earnedUsed = requestedAmount - depositedUsed;
        if (earnedUsed > earnedTokens)
        {
            return null;
        }

        return new BalanceUsage(depositedUsed, earnedUsed);
    }

    /// <summary>
    /// Whether the combined deposited + earned balances can cover the request.
    /// </summary>
    public static bool CanSpend(decimal depositedTokens, decimal earnedTokens, decimal requestedAmount) =>
        requestedAmount > 0m && depositedTokens + earnedTokens >= requestedAmount;
}
