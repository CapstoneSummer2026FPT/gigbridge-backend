using Application.Features.Reports.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Reports.GetReportSummary.Queries;

public record GetReportSummaryQuery : IRequest<ReportSummaryDto>;
