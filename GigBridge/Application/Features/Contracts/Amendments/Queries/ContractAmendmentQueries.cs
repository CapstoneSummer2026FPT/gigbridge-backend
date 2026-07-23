using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Amendments.Common;
using Application.Features.Contracts.Amendments.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Amendments.Queries;

public sealed record GetContractChangeRequestsQuery(Guid ContractId, Guid UserId) : IRequest<IReadOnlyList<ContractChangeRequestDto>>;
public sealed record GetContractAmendmentsQuery(Guid ContractId, Guid UserId) : IRequest<IReadOnlyList<ContractAmendmentDetailDto>>;
public sealed record GetContractAmendmentDetailQuery(Guid ContractId, Guid AmendmentId, Guid UserId) : IRequest<ContractAmendmentDetailDto>;

public sealed class GetContractChangeRequestsQueryHandler : IRequestHandler<GetContractChangeRequestsQuery, IReadOnlyList<ContractChangeRequestDto>>
{
    private readonly IApplicationDbContext _context;
    public GetContractChangeRequestsQueryHandler(IApplicationDbContext context) { _context = context; }
    public async Task<IReadOnlyList<ContractChangeRequestDto>> Handle(GetContractChangeRequestsQuery query, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, query.ContractId, cancellationToken);
        await MilestoneWorkflowGuard.EnsureParticipantAsync(_context, contract, query.UserId, cancellationToken);
        return await _context.Set<ContractChangeRequest>()
            .Where(item => item.ContractsId == query.ContractId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new ContractChangeRequestDto(
                item.ContractChangeRequestId, item.ContractsId, item.RequestedByUserId,
                item.Reason, item.RequestedChanges, item.ResponseNote,
                item.ClarificationRequestNote, item.ClarificationResponseNote,
                item.AffectedMilestoneIds, item.AffectedWorkItemIds,
                item.Status, item.CreatedAt, item.RespondedAt, item.ClarifiedAt,
                item.RequestedByUserId != query.UserId && item.Status == (int)Domain.Enums.ContractChangeRequestStatus.Pending,
                item.RequestedByUserId == query.UserId && item.Status == (int)Domain.Enums.ContractChangeRequestStatus.NeedsClarification))
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetContractAmendmentsQueryHandler : IRequestHandler<GetContractAmendmentsQuery, IReadOnlyList<ContractAmendmentDetailDto>>
{
    private readonly IApplicationDbContext _context;
    public GetContractAmendmentsQueryHandler(IApplicationDbContext context) { _context = context; }
    public async Task<IReadOnlyList<ContractAmendmentDetailDto>> Handle(GetContractAmendmentsQuery query, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, query.ContractId, cancellationToken);
        await MilestoneWorkflowGuard.EnsureParticipantAsync(_context, contract, query.UserId, cancellationToken);
        var amendments = await _context.Set<ContractAmendment>()
            .Include(item => item.Milestones).ThenInclude(item => item.WorkItems)
            .Include(item => item.Signatures)
            .Where(item => item.ContractsId == query.ContractId)
            .OrderByDescending(item => item.RevisionNumber)
            .ToListAsync(cancellationToken);
        return amendments.Select(ContractAmendmentMapper.ToDetail).ToList();
    }
}

public sealed class GetContractAmendmentDetailQueryHandler : IRequestHandler<GetContractAmendmentDetailQuery, ContractAmendmentDetailDto>
{
    private readonly IApplicationDbContext _context;
    public GetContractAmendmentDetailQueryHandler(IApplicationDbContext context) { _context = context; }
    public async Task<ContractAmendmentDetailDto> Handle(GetContractAmendmentDetailQuery query, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, query.ContractId, cancellationToken);
        await MilestoneWorkflowGuard.EnsureParticipantAsync(_context, contract, query.UserId, cancellationToken);
        var amendment = await _context.Set<ContractAmendment>()
            .Include(item => item.Milestones).ThenInclude(item => item.WorkItems)
            .Include(item => item.Signatures)
            .SingleOrDefaultAsync(item => item.ContractAmendmentId == query.AmendmentId && item.ContractsId == query.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract amendment does not exist.");
        return ContractAmendmentMapper.ToDetail(amendment);
    }
}
