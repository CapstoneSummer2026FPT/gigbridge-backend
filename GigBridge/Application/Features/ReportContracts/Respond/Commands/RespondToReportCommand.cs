using Application.Features.ReportContracts.Common.DTOs;
using MediatR;

namespace Application.Features.ReportContracts.Respond.Commands;

public sealed record RespondToReportCommand(
    Guid ContractId,
    Guid ReportId,
    Guid UserId,
    int ResolutionAction,
    string? Explanation,
    string? ProposedResolution,
    string? RejectReason) : IRequest<ReportContractResponse>;
