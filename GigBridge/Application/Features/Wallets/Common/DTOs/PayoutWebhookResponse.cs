namespace Application.Features.Wallets.Common.DTOs;

public sealed record PayoutWebhookResponse(
    Guid WebhookLogId,
    Guid? WithdrawalId,
    int ProcessingStatus);
