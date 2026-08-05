using Application.Features.Admin.Dashboard.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Dashboard.GetSummary.Queries;

public sealed record GetAdminDashboardSummaryQuery(int Days = 30)
    : IRequest<AdminDashboardSummary>;
