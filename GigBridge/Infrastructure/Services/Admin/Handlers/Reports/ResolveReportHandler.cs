using Application.DTOs.Admin;
using Application.Features.Admin.Reports.Resolve;
using Infrastructure.Services.Admin.Interfaces;
using MediatR;

namespace Infrastructure.Services.Admin.Handlers.Reports;

public sealed class ResolveReportHandler : IRequestHandler<ResolveReportCommand, ReportDto>
{
    private readonly IAdminReportService _reportService;

    public ResolveReportHandler(IAdminReportService reportService)
    {
        _reportService = reportService;
    }

    public Task<ReportDto> Handle(ResolveReportCommand request, CancellationToken cancellationToken) =>
        _reportService.ResolveAsync(request.ReportId, request.Request, request.Actor, cancellationToken);
}
