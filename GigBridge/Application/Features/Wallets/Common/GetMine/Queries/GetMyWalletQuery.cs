using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.GetMine.Queries;

public sealed record GetMyWalletQuery(Guid UserId) : IRequest<WalletResponse>;
