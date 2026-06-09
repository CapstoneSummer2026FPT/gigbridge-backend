using Application.Features.Reports.Common.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Reports.GetReports.Queries;

public record GetReportsQuery(
    int Page = 1,
    int PageSize = 20,
    ReportStatus? Status = null,
    ReportType? Type = null,
    string? ReportedEntityType = null,
    string? Search = null) : IRequest<ReportsResponse>;
