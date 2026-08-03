using System.Text.Json;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Wallets.Common;

/// <summary>
/// Builds the balance-source + balance-snapshot audit trail attached to wallet
/// transactions. Kept separate so every wallet-touching flow records the same shape.
/// </summary>
internal static class WalletBalanceAudit
{
    public static WalletBalanceSource ResolveSource(decimal depositedAmount, decimal earnedAmount) =>
        depositedAmount > 0m && earnedAmount > 0m
            ? WalletBalanceSource.Combined
            : earnedAmount > 0m
                ? WalletBalanceSource.Earned
                : WalletBalanceSource.Deposited;

    /// <summary>
    /// Converts a deposited/earned split into nullable transaction breakdown columns.
    /// A source that contributes zero is stored as <see langword="null"/> so the
    /// stored split always reflects exactly which pools the transaction touched.
    /// </summary>
    public static (decimal? DepositedAmount, decimal? EarnedAmount) ToSplitAmounts(
        decimal depositedAmount,
        decimal earnedAmount) =>
        (depositedAmount > 0m ? depositedAmount : null,
         earnedAmount > 0m ? earnedAmount : null);

    /// <summary>
    /// Lightweight copy of a wallet's four balances for the before/after audit trail.
    /// </summary>
    public static UserWallet Snapshot(UserWallet wallet) => new()
    {
        AvailableTokens = wallet.AvailableTokens,
        WithdrawableTokens = wallet.WithdrawableTokens,
        HeldTokens = wallet.HeldTokens,
        PendingWithdrawalTokens = wallet.PendingWithdrawalTokens
    };

    /// <summary>
    /// Merges the deposited/earned split and the pre/post wallet balance snapshots
    /// into the transaction metadata JSON. Existing JSON properties (e.g. a service
    /// fee category) are preserved. Non-JSON metadata is discarded (callers should
    /// not enrich metadata that carries a non-JSON payload).
    /// </summary>
    public static string? EnrichMetadata(
        string? existingMetadata,
        decimal depositedAmount,
        decimal earnedAmount,
        UserWallet before,
        UserWallet after)
    {
        Dictionary<string, object?> payload;
        if (!string.IsNullOrWhiteSpace(existingMetadata) && IsJsonObject(existingMetadata))
        {
            payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(existingMetadata)
                ?? new Dictionary<string, object?>();
        }
        else
        {
            payload = new Dictionary<string, object?>();
        }

        payload["depositedAmount"] = depositedAmount;
        payload["earnedAmount"] = earnedAmount;
        payload["balanceBefore"] = new BalanceSnapshot(
            before.AvailableTokens,
            before.WithdrawableTokens,
            before.HeldTokens,
            before.PendingWithdrawalTokens);
        payload["balanceAfter"] = new BalanceSnapshot(
            after.AvailableTokens,
            after.WithdrawableTokens,
            after.HeldTokens,
            after.PendingWithdrawalTokens);

        return JsonSerializer.Serialize(payload);
    }

    private static bool IsJsonObject(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record BalanceSnapshot(
        decimal Available,
        decimal Withdrawable,
        decimal Held,
        decimal PendingWithdrawal);
}
