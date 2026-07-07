using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.BankAccounts.Get;

public sealed record GetBankAccountsQuery(Guid UserId) : IRequest<IReadOnlyList<BankAccountResponse>>;
