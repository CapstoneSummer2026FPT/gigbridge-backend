using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Elo.Commands.CreateEloAppeal;

/// <summary>
/// Submits an appeal against a single Elo transaction. Text reason required;
/// evidence files optional. Idempotent per (userId, transactionId): re-posting
/// while an appeal is active returns the existing appeal instead of a duplicate.
/// </summary>
public sealed record CreateEloAppealCommand(
    Guid UserId,
    Guid TransactionId,
    string Reason,
    IReadOnlyList<EloAppealFile> Files) : IRequest<EloAppealDto>;
