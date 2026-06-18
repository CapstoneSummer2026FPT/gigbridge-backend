using Application.Features.Reports.Common.DTOs;
using MediatR;

namespace Application.Features.Reports.Public.GetMyReports.Queries;

public record GetMyReportsQuery(Guid UserId, int Page = 1, int PageSize = 20) : IRequest<ReportsResponse>;
