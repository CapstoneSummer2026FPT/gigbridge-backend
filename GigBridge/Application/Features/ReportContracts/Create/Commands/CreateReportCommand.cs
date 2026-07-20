using Application.Features.ReportContracts.Common.DTOs;
using MediatR;

namespace Application.Features.ReportContracts.Create.Commands;

public sealed record CreateReportCommand(
    Guid ContractId,
    Guid UserId,
    int IssueType,
    string Description,
    string DesiredResolution,
    Guid? MilestoneId,
    IReadOnlyList<CreateReportFile> Attachments) : IRequest<ReportContractResponse>;
