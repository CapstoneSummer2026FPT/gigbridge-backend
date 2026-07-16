using Application.Common.Interfaces;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Common.Queries;

public sealed class GetActiveDisputeQueryHandler :
    IRequestHandler<GetActiveDisputeQuery, DisputeResponse?>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public GetActiveDisputeQueryHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<DisputeResponse?> Handle(
        GetActiveDisputeQuery query,
        CancellationToken cancellationToken)
    {
        var contract = await DisputeAccess.GetContractAsync(
            _context,
            query.ContractId,
            cancellationToken);
        await DisputeAccess.EnsureParticipantAsync(
            _context,
            contract,
            query.UserId,
            cancellationToken);

        var disputeId = await _context.Set<Dispute>()
            .AsNoTracking()
            .Where(dispute =>
                dispute.ContractsId == query.ContractId &&
                DisputeAccess.ActiveStatuses.Contains(dispute.Status))
            .OrderByDescending(dispute => dispute.CreatedAt)
            .Select(dispute => (Guid?)dispute.DisputesId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!disputeId.HasValue)
            return null;

        return await _sender.Send(
            new GetDisputeByIdQuery(query.ContractId, disputeId.Value, query.UserId),
            cancellationToken);
    }
}
