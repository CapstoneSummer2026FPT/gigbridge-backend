namespace Application.Features.Wallets.Common.DTOs;

public sealed record CreateWalletTopUpRequest(
    decimal TokenAmount,
    string? ReturnUrl,
    string? CancelUrl,
    string? IdempotencyKey);
