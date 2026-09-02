namespace Application.Features.Wallets.Common.DTOs;

/// <summary>
/// One-shot answer to "why are withdrawals not going out?". Reports the payout provider state, how
/// this node is configured, and the egress address the provider sees. Never carries a credential -
/// only whether one is present and the first 8 characters of the client id, enough to tell two
/// PayOS channels apart.
/// </summary>
public sealed record PayoutHealthResponse(
    string ProviderName,
    string Instance,
    bool WithdrawalsEnabled,
    bool BackgroundWorkersEnabled,
    bool IsAvailable,
    decimal? BalanceVnd,
    string? ErrorCode,
    string? Message,
    bool CredentialsConfigured,
    string? ClientIdPrefix,
    bool ProxyConfigured,
    string? OutboundIp,
    string? OutboundIpError,
    DateTime CheckedAtUtc);
