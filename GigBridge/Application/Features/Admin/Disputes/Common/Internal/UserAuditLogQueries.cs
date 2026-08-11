using Application.Common.Interfaces;
using Application.Features.Admin.Disputes.Common.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Disputes.Common.Internal;

/// <summary>
/// Shared read logic for the Client/Freelancer action audit trail (AuditLogWorkSpace), reused by
/// both the dispute-detail response and the standalone per-contract admin endpoint.
/// </summary>
internal static class UserAuditLogQueries
{
    public static async Task<IReadOnlyList<AdminUserAuditEventResponse>> GetForContractAsync(
        IApplicationDbContext context,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        var logs = await context.Set<AuditLogWorkSpace>()
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Milestone)
            .Where(item => item.ContractId == contractId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return logs.Select(item => new AdminUserAuditEventResponse(
                item.AuditLogWorkSpaceId,
                item.UserId,
                item.User?.FullName,
                item.UserRole,
                item.ActionType,
                item.ContractId,
                item.MilestoneId,
                item.Milestone?.Title,
                item.ReportId,
                item.DisputeId,
                item.Description,
                item.CreatedAt))
            .ToList();
    }
}
