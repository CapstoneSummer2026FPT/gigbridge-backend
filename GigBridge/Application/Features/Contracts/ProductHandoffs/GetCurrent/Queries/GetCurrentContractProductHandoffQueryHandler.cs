using Application.Common.Interfaces;
using Application.Features.Contracts.ProductHandoffs.Common;
using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.ProductHandoffs.GetCurrent.Queries;

public sealed class GetCurrentContractProductHandoffQueryHandler :
    IRequestHandler<GetCurrentContractProductHandoffQuery, ContractProductHandoffResponse?>
{
    private readonly IApplicationDbContext _context;

    public GetCurrentContractProductHandoffQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContractProductHandoffResponse?> Handle(
        GetCurrentContractProductHandoffQuery query,
        CancellationToken cancellationToken)
    {
        var contract = await ContractProductHandoffAccess.GetActiveContractAsync(
            _context,
            query.ContractId,
            cancellationToken);

        await ContractProductHandoffAccess.EnsureParticipantAsync(
            _context,
            contract,
            query.UserId,
            cancellationToken);

        var handoff = await _context.Set<ContractProductHandoff>()
            .AsNoTracking()
            .Where(handoff => handoff.ContractsId == query.ContractId && handoff.IsCurrent)
            .OrderByDescending(handoff => handoff.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return handoff is null ? null : ContractProductHandoffMapper.ToResponse(handoff);
    }
}
