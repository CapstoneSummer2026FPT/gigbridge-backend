using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Webhook;

public sealed record HandlePayoutWebhookCommand(
    string RawPayload,
    string? Signature) : IRequest<PayoutWebhookResponse>;
