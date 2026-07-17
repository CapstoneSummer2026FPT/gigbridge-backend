using Application.Common.Interfaces;
using Application.Features.ReportContracts.Common.DTOs;
using Application.Features.ReportContracts.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ReportContracts.Common.Queries;

public sealed class GetContractReportsQueryHandler :
    IRequestHandler<GetContractReportsQuery, IReadOnlyList<ReportContractListResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetContractReportsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ReportContractListResponse>> Handle(
        GetContractReportsQuery query,
        CancellationToken cancellationToken)
    {
        var contract = await ReportContractAccess.GetContractAsync(
            _context,
            query.ContractId,
            cancellationToken);
        var participants = await ReportContractAccess.EnsureParticipantAsync(
            _context,
            contract,
            query.UserId,
            cancellationToken);

        var reports = await _context.Set<ReportContract>()
            .AsNoTracking()
            .Where(r => r.ContractId == query.ContractId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        if (reports.Count == 0)
            return Array.Empty<ReportContractListResponse>();

        var reporterIds = reports.Select(r => r.ReporterId).Distinct().ToHashSet();
        var users = await _context.Set<User>()
            .AsNoTracking()
            .Where(u => reporterIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        return reports.Select(r => new ReportContractListResponse(
            r.ReportContractId,
            r.ReporterId,
            users.GetValueOrDefault(r.ReporterId),
            participants.GetRole(r.ReporterId),
            r.IssueType,
            r.Status,
            r.ResolutionAction,
            r.CreatedAt,
            r.RespondedAt,
            r.ResolvedAt)).ToList();
    }
}
