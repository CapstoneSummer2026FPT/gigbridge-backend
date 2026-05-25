using Application.DTOs.Admin;
using MediatR;

namespace Application.Features.Admin.Dashboard.GetSummary;

public sealed record GetAdminDashboardQuery : IRequest<DashboardSummaryDto>;
