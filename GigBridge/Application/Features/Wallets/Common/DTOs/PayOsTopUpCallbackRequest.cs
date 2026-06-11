namespace Application.Features.Wallets.Common.DTOs;

public sealed record PayOsTopUpCallbackRequest(
    long? OrderCode,
    bool? Success,
    string? Code,
    string? Desc,
    string? GatewayTransactionCode,
    decimal? AmountVnd,
    PayOsTopUpCallbackData? Data);

public sealed record PayOsTopUpCallbackData(
    long? OrderCode,
    decimal? Amount,
    string? Reference,
    string? PaymentLinkId);
