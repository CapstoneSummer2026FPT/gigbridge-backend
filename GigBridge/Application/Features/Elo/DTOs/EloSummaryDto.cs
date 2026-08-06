using System;
using System.Collections.Generic;

namespace Application.Features.Elo.DTOs;

/// <summary>
/// Headline Elo state for a user: the current score plus lifetime gained/lost
/// totals and the most recent transactions for the profile gauge header.
/// </summary>
public sealed record EloSummaryDto(
    int CurrentPoints,
    int TotalGained,
    int TotalLost,
    int TotalTransactions,
    IReadOnlyList<EloTransactionDto> RecentTransactions);
