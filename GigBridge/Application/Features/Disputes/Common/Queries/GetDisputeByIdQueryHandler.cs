using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Common.Queries;

public sealed class GetDisputeByIdQueryHandler :
    IRequestHandler<GetDisputeByIdQuery, DisputeResponse>
{
    private static readonly Dictionary<int, string> ResolutionLabels = new()
    {
        [(int)DisputeResolution.ClientFavored] = "Client Favored",
        [(int)DisputeResolution.FreelancerFavored] = "Freelancer Favored",
        [(int)DisputeResolution.Split] = "Split",
        [(int)DisputeResolution.Dismissed] = "Dismissed"
    };

    private readonly IApplicationDbContext _context;

    public GetDisputeByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DisputeResponse> Handle(
        GetDisputeByIdQuery query,
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

        var dispute = await _context.Set<Dispute>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                    item.DisputesId == query.DisputeId &&
                    item.ContractsId == query.ContractId,
                cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        var initiatorName = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == dispute.InitiatorId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        string? milestoneTitle = null;
        if (dispute.MilestonesId.HasValue)
        {
            milestoneTitle = await _context.Set<Milestone>()
                .AsNoTracking()
                .Where(milestone => milestone.MilestonesId == dispute.MilestonesId.Value)
                .Select(milestone => milestone.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var evidences = await _context.Set<DisputeEvidence>()
            .AsNoTracking()
            .Where(evidence => evidence.DisputesId == dispute.DisputesId)
            .OrderBy(evidence => evidence.CreatedAt)
            .Select(evidence => new DisputeEvidenceResponse(
                evidence.DisputeEvidenceId,
                evidence.UploadedById,
                evidence.FileName,
                evidence.FileSize,
                evidence.Description,
                evidence.CreatedAt))
            .ToListAsync(cancellationToken);

        return new DisputeResponse(
            dispute.DisputesId,
            dispute.ContractsId,
            dispute.InitiatorId,
            initiatorName,
            participants.GetRole(dispute.InitiatorId),
            dispute.MilestonesId,
            milestoneTitle,
            dispute.Reason,
            dispute.Status,
            dispute.Resolution,
            dispute.Resolution.HasValue
                ? ResolutionLabels.GetValueOrDefault(dispute.Resolution.Value)
                : null,
            dispute.ResolutionNote,
            dispute.ResolvedAt,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            evidences);
    }
}
