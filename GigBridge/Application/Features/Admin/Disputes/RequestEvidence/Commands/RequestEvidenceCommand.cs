using Application.Features.Admin.Disputes.Common.DTOs;
using Domain.Enums.Disputes;
using MediatR;

namespace Application.Features.Admin.Disputes.RequestEvidence.Commands;

public sealed record RequestEvidenceCommand(
    Guid DisputeId,
    Guid AdminId,
    string Reason,
    DateTime? Deadline,
    EvidenceRequestTarget Target) : IRequest<AdminDisputeDetailResponse>;
