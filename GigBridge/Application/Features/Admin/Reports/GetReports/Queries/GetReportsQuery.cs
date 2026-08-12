using Application.Features.Reports.Common.DTOs;
using Domain.Enums.Reports;
using MediatR;

namespace Application.Features.Admin.Reports.GetReports.Queries;

public record GetReportsQuery(
    int Page = 1,
    int PageSize = 20,
    ReportStatus? Status = null,
    ReportType? Type = null,
    string? ReportedEntityType = null,
    Guid? ReportedEntityId = null,
    string? Search = null,
    bool? IsPremium = null) : IRequest<ReportsResponse>;
