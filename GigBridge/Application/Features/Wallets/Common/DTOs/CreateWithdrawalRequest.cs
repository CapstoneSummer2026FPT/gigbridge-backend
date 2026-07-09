namespace Application.Features.Wallets.Common.DTOs;

public sealed record CreateWithdrawalRequest(
    decimal TokenAmount,
    Guid? BankAccountId,
    string? IdempotencyKey);
