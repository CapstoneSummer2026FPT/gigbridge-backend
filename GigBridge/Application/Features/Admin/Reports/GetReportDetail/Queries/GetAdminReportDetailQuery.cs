using Application.Features.Reports.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Reports.GetReportDetail.Queries;

public record GetAdminReportDetailQuery(Guid ReportId) : IRequest<ReportDto>;
