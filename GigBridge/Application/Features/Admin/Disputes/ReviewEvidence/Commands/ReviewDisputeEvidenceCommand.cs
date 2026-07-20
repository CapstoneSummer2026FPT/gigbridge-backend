using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Disputes.ReviewEvidence.Commands;

public sealed record ReviewDisputeEvidenceCommand(
    Guid DisputeId,
    Guid EvidenceId,
    Guid AdminId,
    string? ReviewNote) : IRequest<DisputeEvidenceResponse>;
