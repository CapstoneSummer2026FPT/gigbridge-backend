using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Common.Queries;

public sealed record GetDisputeEvidenceDownloadQuery(
    Guid ContractId,
    Guid DisputeId,
    Guid EvidenceId,
    Guid UserId) : IRequest<DisputeEvidenceDownloadResponse>;
