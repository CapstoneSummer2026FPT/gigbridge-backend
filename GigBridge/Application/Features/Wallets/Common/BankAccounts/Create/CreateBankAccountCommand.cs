using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.BankAccounts.Create;

public sealed record CreateBankAccountCommand(
    Guid UserId,
    CreateBankAccountRequest Request) : IRequest<BankAccountResponse>;
