using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.BankAccounts.Update;

public sealed record UpdateBankAccountCommand(
    Guid UserId,
    Guid BankAccountId,
    UpdateBankAccountRequest Request) : IRequest<BankAccountResponse>;
