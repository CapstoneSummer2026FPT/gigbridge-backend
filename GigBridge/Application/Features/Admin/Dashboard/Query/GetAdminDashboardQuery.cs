using Application.Features.Admin.Dashboard.Dto;
using MediatR;

namespace Application.Features.Admin.Dashboard.Query;

public sealed record GetAdminDashboardQuery : IRequest<DashboardSummaryDto>;
