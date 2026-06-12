using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.TopUps.Create.Commands;

public sealed record CreateWalletTopUpCommand(
    Guid UserId,
    CreateWalletTopUpRequest Request) : IRequest<CreateWalletTopUpResponse>;
