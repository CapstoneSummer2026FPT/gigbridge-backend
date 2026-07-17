using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ReportContracts.Common.DTOs;
using Application.Features.ReportContracts.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ReportContracts.Common.Queries;

public sealed class GetReportByIdQueryHandler :
    IRequestHandler<GetReportByIdQuery, ReportContractResponse>
{
    private readonly IApplicationDbContext _context;

    public GetReportByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportContractResponse> Handle(
        GetReportByIdQuery query,
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

        var report = await _context.Set<ReportContract>()
            .AsNoTracking()
            .Include(r => r.ReportContractAttachments)
            .FirstOrDefaultAsync(r => r.ReportContractId == query.ReportId, cancellationToken)
            ?? throw new NotFoundException("Report does not exist.");

        if (report.ContractId != query.ContractId)
        {
            throw new BadRequestException("The report does not belong to this contract.");
        }

        var reporter = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == report.ReporterId, cancellationToken);

        User? respondent = null;
        if (report.RespondentId.HasValue)
        {
            respondent = await _context.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == report.RespondentId.Value, cancellationToken);
        }

        string? milestoneTitle = null;
        if (report.MilestoneId.HasValue)
        {
            milestoneTitle = await _context.Set<Milestone>()
                .AsNoTracking()
                .Where(m => m.MilestonesId == report.MilestoneId.Value)
                .Select(m => m.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var attachments = report.ReportContractAttachments
            .OrderBy(a => a.UploadedAt)
            .Select(a => new ReportContractAttachmentResponse(
                a.ReportContractAttachmentId,
                a.FileUrl,
                a.FileName,
                a.ContentType,
                a.FileSize,
                a.UploadedAt))
            .ToList();

        return new ReportContractResponse(
            report.ReportContractId,
            report.ContractId,
            report.ReporterId,
            reporter?.FullName,
            participants.GetRole(report.ReporterId),
            report.RespondentId,
            respondent?.FullName,
            report.RespondentId.HasValue ? participants.GetRole(report.RespondentId.Value) : null,
            report.MilestoneId,
            milestoneTitle,
            report.IssueType,
            report.Description,
            report.DesiredResolution,
            report.Status,
            report.ResolutionAction,
            report.Explanation,
            report.ProposedResolution,
            report.RejectReason,
            report.ResolvedBy,
            report.CreatedAt,
            report.RespondedAt,
            report.ResolvedAt,
            report.IsEscalatedToDispute,
            attachments);
    }
}
