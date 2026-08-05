using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Elo.Queries.GetEloTransactionDetail;

/// <summary>
/// Loads a single Elo transaction owned by <see cref="UserId"/>. The controller
/// resolves the current user so a user can never read another user's ledger row.
/// </summary>
public sealed record GetEloTransactionDetailQuery(
    Guid TransactionId,
    Guid UserId) : IRequest<EloTransactionDetailDto>;
