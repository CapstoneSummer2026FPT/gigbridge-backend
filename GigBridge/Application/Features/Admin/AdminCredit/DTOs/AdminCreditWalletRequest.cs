namespace Application.Features.Admin.AdminCredit.DTOs;

public sealed record AdminCreditWalletRequest(
    decimal TokenAmount,
    string? Note,
    string? IdempotencyKey);
