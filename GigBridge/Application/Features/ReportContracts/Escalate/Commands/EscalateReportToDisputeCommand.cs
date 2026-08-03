using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Domain.Enums;
using MediatR;

namespace Application.Features.ReportContracts.Escalate.Commands;

/// <summary>
/// Escalate a report into an official dispute.
/// The reporter rejects the proposed resolution and escalates.
/// This sets Report.Status = Escalated, creates a Dispute with Open status,
/// sets Contract.Status = Disputed, and creates a Dispute conversation.
/// </summary>
public sealed record EscalateReportToDisputeCommand(
    Guid ContractId,
    Guid ReportId,
    Guid UserId,
    string Title,
    string Description,
    decimal? ClaimedAmount,
    string RequestedResolution,
    DisputeUrgency? Urgency,
    bool DeclarationAccepted,
    IReadOnlyList<DisputeEvidenceFile> EvidenceFiles,
    Guid? AdminActorId = null,
    string? AdminReason = null) : IRequest<DisputeResponse>;
