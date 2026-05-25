using Application.DTOs.Admin;
using Application.Features.Admin.Reports.StartReview;
using Infrastructure.Services.Admin.Interfaces;
using MediatR;

namespace Infrastructure.Services.Admin.Handlers.Reports;

public sealed class StartReportReviewHandler : IRequestHandler<StartReportReviewCommand, ReportDto>
{
    private readonly IAdminReportService _reportService;

    public StartReportReviewHandler(IAdminReportService reportService)
    {
        _reportService = reportService;
    }

    public Task<ReportDto> Handle(StartReportReviewCommand request, CancellationToken cancellationToken) =>
        _reportService.ReviewAsync(request.ReportId, request.Request, request.Actor, cancellationToken);
}
