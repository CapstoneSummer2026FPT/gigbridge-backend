using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.TopUps.Sync.Commands;

public sealed record SyncWalletTopUpCommand(
    Guid UserId,
    SyncPayOsTopUpRequest Request) : IRequest<WalletTransactionResponse>;
