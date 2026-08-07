namespace Application.Features.Elo.DTOs;

/// <summary>
/// Transaction detail: the ledger row plus the active appeal (if any) so the UI
/// can surface "you already appealed this change" without a second request.
/// </summary>
public sealed record EloTransactionDetailDto(
    EloTransactionDto Transaction,
    EloAppealDto? ActiveAppeal);
