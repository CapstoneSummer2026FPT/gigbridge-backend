using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Admin.Disputes.Common.Internal;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Disputes.DownloadEvidence.Queries;

public sealed class GetAdminDisputeEvidenceDownloadQueryHandler :
    IRequestHandler<GetAdminDisputeEvidenceDownloadQuery, DisputeEvidenceDownloadResponse>
{
    private readonly IApplicationDbContext _context;

    public GetAdminDisputeEvidenceDownloadQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DisputeEvidenceDownloadResponse> Handle(
        GetAdminDisputeEvidenceDownloadQuery query,
        CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(
            _context,
            query.AdminId,
            cancellationToken);

        var evidence = await (
                from dispute in _context.Set<Dispute>().AsNoTracking()
                join item in _context.Set<DisputeEvidence>().AsNoTracking()
                    on dispute.DisputesId equals item.DisputesId
                where dispute.DisputesId == query.DisputeId &&
                      item.DisputeEvidenceId == query.EvidenceId
                select item)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Dispute evidence does not exist.");

        if (!evidence.IsRequestFulfilled && evidence.IsRequestedByAdmin ||
            string.IsNullOrWhiteSpace(evidence.FileUrl) ||
            string.IsNullOrWhiteSpace(evidence.FileName))
            throw new BadRequestException("Dispute evidence does not have a downloadable URL.");

        return new DisputeEvidenceDownloadResponse(
            evidence.DisputeEvidenceId,
            evidence.FileName,
            evidence.FileUrl);
    }
}
