using Application.Common.Interfaces;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Common.Queries;

public sealed class GetContractDisputesQueryHandler :
    IRequestHandler<GetContractDisputesQuery, IReadOnlyList<DisputeResponse>>
{
    private static readonly Dictionary<int, string> ResolutionLabels = new()
    {
        [(int)DisputeResolution.ClientFavored] = "Client Favored",
        [(int)DisputeResolution.FreelancerFavored] = "Freelancer Favored",
        [(int)DisputeResolution.Split] = "Split",
        [(int)DisputeResolution.Dismissed] = "Dismissed"
    };

    private readonly IApplicationDbContext _context;

    public GetContractDisputesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DisputeResponse>> Handle(
        GetContractDisputesQuery query,
        CancellationToken cancellationToken)
    {
        var contract = await DisputeAccess.GetContractAsync(
            _context,
            query.ContractId,
            cancellationToken);
        var participants = await DisputeAccess.EnsureParticipantAsync(
            _context,
            contract,
            query.UserId,
            cancellationToken);

        var disputes = await _context.Set<Dispute>()
            .AsNoTracking()
            .Where(dispute => dispute.ContractsId == query.ContractId)
            .OrderByDescending(dispute => dispute.CreatedAt)
            .ToListAsync(cancellationToken);

        if (disputes.Count == 0)
            return Array.Empty<DisputeResponse>();

        var allUserIds = disputes
            .SelectMany(d => new Guid?[] { d.InitiatorId, d.RespondentId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToHashSet();
        var users = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => allUserIds.Contains(user.UserId))
            .ToDictionaryAsync(user => user.UserId, user => user.FullName, cancellationToken);

        var milestoneIds = disputes
            .Where(dispute => dispute.MilestonesId.HasValue)
            .Select(dispute => dispute.MilestonesId!.Value)
            .Distinct()
            .ToHashSet();
        var milestones = milestoneIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Set<Milestone>()
                .AsNoTracking()
                .Where(milestone => milestoneIds.Contains(milestone.MilestonesId))
                .ToDictionaryAsync(milestone => milestone.MilestonesId, milestone => milestone.Title, cancellationToken);

        var disputeIds = disputes.Select(dispute => dispute.DisputesId).ToHashSet();
        var reportIds = disputes
            .Where(dispute => dispute.RelatedReportId.HasValue)
            .Select(dispute => dispute.RelatedReportId!.Value)
            .Distinct()
            .ToHashSet();
        var issueTypes = reportIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _context.Set<ReportContract>()
                .AsNoTracking()
                .Where(report => reportIds.Contains(report.ReportContractId))
                .ToDictionaryAsync(
                    report => report.ReportContractId,
                    report => report.IssueType,
                    cancellationToken);
        var evidenceByDispute = (await _context.Set<DisputeEvidence>()
                .AsNoTracking()
                .Where(evidence => disputeIds.Contains(evidence.DisputesId))
                .OrderBy(evidence => evidence.CreatedAt)
                .ToListAsync(cancellationToken))
            .GroupBy(evidence => evidence.DisputesId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return disputes.Select(dispute =>
        {
            int? issueType = null;
            if (dispute.RelatedReportId.HasValue &&
                issueTypes.TryGetValue(dispute.RelatedReportId.Value, out var resolvedIssueType))
            {
                issueType = resolvedIssueType;
            }

            var evidenceResponses = evidenceByDispute
                .GetValueOrDefault(dispute.DisputesId, [])
                .Select(evidence => new DisputeEvidenceResponse(
                    evidence.DisputeEvidenceId,
                    evidence.UploadedById,
                    evidence.FileName,
                    evidence.FileSize,
                    evidence.Description,
                    evidence.CreatedAt))
                .ToList();

            return new DisputeResponse(
                dispute.DisputesId,
                dispute.ContractsId,
                dispute.InitiatorId,
                users.GetValueOrDefault(dispute.InitiatorId),
                participants.GetRole(dispute.InitiatorId),
                dispute.RespondentId,
                dispute.RespondentId.HasValue ? users.GetValueOrDefault(dispute.RespondentId.Value) : null,
                dispute.RespondentId.HasValue ? participants.GetRole(dispute.RespondentId.Value) : null,
                dispute.MilestonesId,
                dispute.MilestonesId.HasValue
                    ? milestones.GetValueOrDefault(dispute.MilestonesId.Value)
                    : null,
                dispute.RelatedReportId,
                dispute.Title,
                dispute.Description,
                dispute.Reason,
                dispute.ClaimedAmount,
                dispute.RequestedResolution,
                issueType,
                dispute.Urgency,
                dispute.Status,
                dispute.Resolution,
                dispute.Resolution.HasValue
                    ? ResolutionLabels.GetValueOrDefault(dispute.Resolution.Value)
                    : null,
                dispute.ResolutionNote,
                dispute.ResolvedAt,
                dispute.CreatedAt,
                dispute.UpdatedAt,
                dispute.OpenedAt,
                evidenceResponses);
        }).ToList();
    }
}
