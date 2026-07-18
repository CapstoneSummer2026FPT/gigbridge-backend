using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Disputes.DownloadEvidence.Queries;

public sealed record GetAdminDisputeEvidenceDownloadQuery(
    Guid DisputeId,
    Guid EvidenceId,
    Guid AdminId) : IRequest<DisputeEvidenceDownloadResponse>;
