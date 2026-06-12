namespace Application.Features.Wallets.Common.DTOs;

public sealed record CreateWalletTopUpResponse(
    Guid WalletTransactionId,
    decimal TokenAmount,
    decimal AmountVnd,
    string GatewayProvider,
    string GatewayOrderCode,
    string? GatewayTransactionCode,
    string? CheckoutUrl,
    int Status);
