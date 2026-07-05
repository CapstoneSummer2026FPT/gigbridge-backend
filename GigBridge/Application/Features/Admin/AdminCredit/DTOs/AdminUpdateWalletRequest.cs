namespace Application.Features.Admin.AdminCredit.DTOs;

public sealed record AdminUpdateWalletRequest(
    decimal TokenAmount,
    string? Note,
    string? IdempotencyKey);
