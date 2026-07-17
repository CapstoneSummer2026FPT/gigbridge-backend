using Application.Features.ReportContracts.Common.DTOs;
using MediatR;

namespace Application.Features.ReportContracts.Confirm.Commands;

/// <summary>
/// Confirm or decline the resolution proposed by the respondent.
/// If <see cref="IsAccepted"/> is false, the report stays in WaitingReporterConfirmation status
/// and may later be escalated to a Dispute.
/// </summary>
public sealed record ConfirmResolutionCommand(
    Guid ContractId,
    Guid ReportId,
    Guid UserId,
    bool IsAccepted) : IRequest<ReportContractResponse>;
