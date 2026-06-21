using Application.Features.Reports.Common.DTOs;
using MediatR;

namespace Application.Features.Reports.Public.GetReportDetail.Queries;

public record GetReportDetailQuery(Guid ReportId, Guid UserId) : IRequest<ReportDto>;
