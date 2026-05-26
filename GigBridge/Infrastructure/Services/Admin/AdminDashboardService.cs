using Application.Features.Admin.Dashboard.Dto;
using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public sealed class AdminDashboardService : AdminServiceBase, IAdminDashboardService
{
    public AdminDashboardService(IApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        return new DashboardSummaryDto
        {
            OpenJobs = await DbContext.Set<JobPost>().CountAsync(x => x.Status == 1, cancellationToken),
            PendingReports = await DbContext.Set<Report>().CountAsync(x => x.Status == 0, cancellationToken),
            OpenDisputes = await DbContext.Set<Dispute>().CountAsync(x => x.Status == 0, cancellationToken),
            HiddenReviews = await DbContext.Set<Review>().CountAsync(x => x.IsVisible == false, cancellationToken)
        };
    }
}

