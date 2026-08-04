using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Common.Queries;

/// <summary>
/// Returns the active dispute (Open or UnderReview) for a contract, or null if none exists.
/// </summary>
public sealed record GetActiveDisputeQuery(
    Guid ContractId,
    Guid UserId) : IRequest<DisputeResponse?>;
