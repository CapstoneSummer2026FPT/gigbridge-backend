namespace Application.Features.Wallets.Common.DTOs;

public sealed record WithdrawalSettingsResponse(
    bool Enabled,
    decimal VndPerToken,
    decimal FixedFeeVnd,
    decimal MinTokens,
    decimal MaxTokens,
    decimal DailyMaxTokens,
    string Provider);
