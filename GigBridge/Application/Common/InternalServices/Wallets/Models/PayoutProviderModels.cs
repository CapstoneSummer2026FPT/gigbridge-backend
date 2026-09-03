namespace Application.Common.InternalServices.Wallets.Models;
public enum PayoutProviderOutcome
{
    Accepted = 0,
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    SyncRequired = 4
}

public sealed record PayoutCreateRequest(
    Guid WithdrawalId,
    string ProviderOrderCode,
    decimal AmountVnd,
    string BankBin,
    string AccountNumber,
    string AccountName,
    string Description,
    string IdempotencyKey);

public sealed record PayoutStatusRequest(
    Guid WithdrawalId,
    string ProviderOrderCode,
    string? ProviderPayoutId);

public sealed record PayoutProviderResult(
    PayoutProviderOutcome Outcome,
    string? ProviderPayoutId,
    string? ProviderTransactionCode,
    string? RawStatus,
    string? FailureReason);

public sealed record PayoutProviderAvailability(
    bool IsAvailable,
    decimal? BalanceVnd,
    string? ErrorCode,
    string? SafeMessage);

/// <summary>
/// Node-level facts about the payout client. <paramref name="OutboundIp"/> is the address the
/// payout provider actually sees, measured through the same HTTP handler the payout client uses,
/// so it accounts for a NAT gateway or a configured proxy. It is what a provider IP allowlist has
/// to contain - not the address the host believes it has.
/// </summary>
public sealed record PayoutProviderDiagnostics(
    bool CredentialsConfigured,
    string? ClientIdPrefix,
    bool ProxyConfigured,
    string? OutboundIp,
    string? OutboundIpError);
