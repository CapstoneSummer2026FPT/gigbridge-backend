using MediatR;

namespace Application.Features.Wallets.Common.BankAccounts.Delete;

public sealed record DeleteBankAccountCommand(Guid UserId, Guid BankAccountId) : IRequest;
