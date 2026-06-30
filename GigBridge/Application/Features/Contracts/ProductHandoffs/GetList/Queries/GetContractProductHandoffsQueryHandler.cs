using Application.Common.Interfaces;
using Application.Features.Contracts.ProductHandoffs.Common;
using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.ProductHandoffs.GetList.Queries;

public sealed class GetContractProductHandoffsQueryHandler :
    IRequestHandler<GetContractProductHandoffsQuery, IReadOnlyList<ContractProductHandoffResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetContractProductHandoffsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ContractProductHandoffResponse>> Handle(
        GetContractProductHandoffsQuery query,
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

        var handoffs = await _context.Set<ContractProductHandoff>()
            .AsNoTracking()
            .Where(handoff => handoff.ContractsId == query.ContractId)
            .OrderByDescending(handoff => handoff.IsCurrent)
            .ThenByDescending(handoff => handoff.Version)
            .ToListAsync(cancellationToken);

        return handoffs.Select(ContractProductHandoffMapper.ToResponse).ToList();
    }
}
