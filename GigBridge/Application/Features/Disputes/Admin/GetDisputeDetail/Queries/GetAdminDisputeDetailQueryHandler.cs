using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Admin.GetDisputeDetail.Queries;

public sealed class GetAdminDisputeDetailQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAdminDisputeDetailQuery, AdminDisputeDto>
{
    public async Task<AdminDisputeDto> Handle(
        GetAdminDisputeDetailQuery query,
        CancellationToken cancellationToken)
    {
        return await context.Set<Dispute>().AsNoTracking()
            .Where(x => x.DisputesId == query.DisputeId)
            .Select(x => new AdminDisputeDto(
                x.DisputesId, x.ContractsId, x.InitiatorId, x.MilestonesId,
                x.Reason, x.Status, x.Resolution, x.ResolutionNote,
                x.IsVipPriority, x.ResolutionTargetAt, x.AiAnalysisStatus.ToString(),
                x.AiSuggestedResolution, x.CreatedAt, x.ResolvedAt, x.ResolvedByAdminId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Dispute not found.");
    }
}
