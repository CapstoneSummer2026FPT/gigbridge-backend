using Application.DTOs.Admin;
using MediatR;

namespace Application.Features.Admin.Reports.Resolve;

public sealed record ResolveReportCommand(
    Guid ReportId,
    ReportResolutionRequestDto Request,
    AdminActorDto Actor) : IRequest<ReportDto>;
