using Application.Features.Admin.Reports.ResolveReport.DTOs;
using MediatR;

namespace Application.Features.Admin.Reports.ResolveReport.Commands;

public record ResolveReportCommand(Guid ReportId, Guid AdminId, ResolveReportRequest Request) : IRequest;
