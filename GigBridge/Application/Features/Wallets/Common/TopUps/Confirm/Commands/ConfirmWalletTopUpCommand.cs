using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.TopUps.Confirm.Commands;

public sealed record ConfirmWalletTopUpCommand(PayOsTopUpCallbackRequest Request) : IRequest<WalletTransactionResponse>;
