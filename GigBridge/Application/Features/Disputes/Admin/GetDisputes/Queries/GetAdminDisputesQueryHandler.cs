using Application.Common.Interfaces;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Admin.GetDisputes.Queries;

public sealed class GetAdminDisputesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAdminDisputesQuery, IReadOnlyList<AdminDisputeDto>>
{
    public async Task<IReadOnlyList<AdminDisputeDto>> Handle(
        GetAdminDisputesQuery query,
        CancellationToken cancellationToken) =>
        await context.Set<Dispute>().AsNoTracking()
            .OrderByDescending(x => x.Status < 2 && x.IsVipPriority)
            .ThenBy(x => x.ResolutionTargetAt)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new AdminDisputeDto(
                x.DisputesId, x.ContractsId, x.InitiatorId, x.MilestonesId,
                x.Reason, x.Status, x.Resolution, x.ResolutionNote,
                x.IsVipPriority, x.ResolutionTargetAt, x.AiAnalysisStatus.ToString(),
                x.AiSuggestedResolution, x.CreatedAt, x.ResolvedAt, x.ResolvedByAdminId))
            .ToListAsync(cancellationToken);
}
