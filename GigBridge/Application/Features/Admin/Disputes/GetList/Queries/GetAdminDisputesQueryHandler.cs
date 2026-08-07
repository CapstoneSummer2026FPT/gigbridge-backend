using Application.Common.Interfaces;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Disputes.GetList.Queries;

public sealed class GetAdminDisputesQueryHandler :
    IRequestHandler<GetAdminDisputesQuery, AdminDisputeListResponse>
{
    private readonly IApplicationDbContext _context;

    public GetAdminDisputesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDisputeListResponse> Handle(
        GetAdminDisputesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = _context.Set<Dispute>().AsNoTracking().AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(dispute => dispute.Status == (int)request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var trimmedSearch = request.Search.Trim();
            var keyword = $"%{trimmedSearch}%";
            var isGuid = Guid.TryParse(trimmedSearch, out var searchedId);
            query = query.Where(dispute =>
                EF.Functions.Like(dispute.Reason, keyword) ||
                EF.Functions.Like(dispute.Contracts.Title, keyword) ||
                EF.Functions.Like(dispute.Initiator.FullName, keyword) ||
                EF.Functions.Like(dispute.Contracts.ClientProfiles.User.FullName, keyword) ||
                (dispute.Contracts.FreelancerProfiles != null &&
                 EF.Functions.Like(dispute.Contracts.FreelancerProfiles.User.FullName, keyword)) ||
                (isGuid && (dispute.DisputesId == searchedId || dispute.ContractsId == searchedId)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(dispute => dispute.Status)
            .ThenByDescending(dispute => dispute.CreatedAt)
            .ThenBy(dispute => dispute.DisputesId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(dispute => new
            {
                Dispute = dispute,
                ContractTitle = dispute.Contracts.Title,
                InitiatorName = dispute.Initiator.FullName,
                ClientUserId = dispute.Contracts.ClientProfiles.UserId,
                ClientName = dispute.Contracts.ClientProfiles.User.FullName,
                FreelancerUserId = dispute.Contracts.FreelancerProfiles == null
                    ? (Guid?)null
                    : dispute.Contracts.FreelancerProfiles.UserId,
                FreelancerName = dispute.Contracts.FreelancerProfiles == null
                    ? null
                    : dispute.Contracts.FreelancerProfiles.User.FullName,
                MilestoneTitle = dispute.Milestones == null ? null : dispute.Milestones.Title,
                EvidenceCount = dispute.DisputeEvidences.Count(e => e.FileName != null)
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new AdminDisputeListItemResponse(
            row.Dispute.DisputesId,
            row.Dispute.ContractsId,
            row.ContractTitle,
            row.InitiatorName,
            row.Dispute.InitiatorId == row.ClientUserId
                ? "Client"
                : row.FreelancerUserId == row.Dispute.InitiatorId ? "Freelancer" : null,
            row.ClientName,
            row.FreelancerName,
            row.Dispute.MilestonesId,
            row.MilestoneTitle,
            row.Dispute.Reason,
            row.Dispute.Status,
            row.Dispute.Resolution,
            AdminDisputeSupport.GetResolutionLabel(row.Dispute.Resolution),
            row.EvidenceCount,
            row.Dispute.CreatedAt,
            row.Dispute.UpdatedAt,
            row.Dispute.ResolvedAt)).ToList();

        return new AdminDisputeListResponse(
            items,
            page,
            pageSize,
            totalItems,
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize));
    }
}
