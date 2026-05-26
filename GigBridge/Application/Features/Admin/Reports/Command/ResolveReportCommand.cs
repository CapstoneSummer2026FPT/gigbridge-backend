using Application.DTOs.Admin;
using Application.Features.Admin.Reports.Dto;
using MediatR;

namespace Application.Features.Admin.Reports.Command;

public sealed record ResolveReportCommand(
    Guid ReportId,
    ReportResolutionRequestDto Request,
    AdminActorDto Actor) : IRequest<ReportDto>;
