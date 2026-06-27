using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Admin.Cheating.DTOs;
using Application.Features.Admin.Cheating.GetViolations.Queries;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Cheating.GetViolationDetail.Queries;

public class GetAdminCheatingViolationDetailQueryHandler
    : IRequestHandler<GetAdminCheatingViolationDetailQuery, AdminCheatingViolationDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminCheatingViolationDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminCheatingViolationDetailDto> Handle(
        GetAdminCheatingViolationDetailQuery request,
        CancellationToken cancellationToken)
    {
        var violation = await _context.Set<FreelancerCheatingViolation>()
            .AsNoTracking()
            .Include(existingViolation => existingViolation.FreelancerUser)
            .Include(existingViolation => existingViolation.ReviewedByAdmin)
            .Include(existingViolation => existingViolation.Proposals)
                .ThenInclude(proposal => proposal.JobPosts)
            .FirstOrDefaultAsync(
                existingViolation => existingViolation.FreelancerCheatingViolationsId == request.ViolationId,
                cancellationToken);

        if (violation is null)
        {
            throw new NotFoundException("Cheating violation does not exist.");
        }

        var baseDto = GetAdminCheatingViolationsQueryHandler.ToDto(violation);
        var events = await _context.Set<ProposalCheatingEvent>()
            .AsNoTracking()
            .Where(cheatingEvent => cheatingEvent.ProposalsId == violation.ProposalsId)
            .OrderBy(cheatingEvent => cheatingEvent.CreatedAt)
            .Select(cheatingEvent => new AdminCheatingEventDto
            {
                ProposalCheatingEventId = cheatingEvent.ProposalCheatingEventsId,
                ProposalId = cheatingEvent.ProposalsId,
                FreelancerUserId = cheatingEvent.FreelancerUserId,
                FreelancerName = cheatingEvent.FreelancerUser.FullName,
                FreelancerEmail = cheatingEvent.FreelancerUser.Email,
                JobPostId = cheatingEvent.Proposals.JobPostsId,
                JobTitle = cheatingEvent.Proposals.JobPosts.Title,
                JobPostQuestionId = cheatingEvent.JobPostQuestionsId,
                EventType = cheatingEvent.EventType,
                ClientEventId = cheatingEvent.ClientEventId,
                IpAddress = cheatingEvent.IpAddress,
                UserAgent = cheatingEvent.UserAgent,
                Metadata = cheatingEvent.Metadata,
                OccurredAt = cheatingEvent.OccurredAt,
                CreatedAt = cheatingEvent.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminCheatingViolationDetailDto
        {
            FreelancerCheatingViolationId = baseDto.FreelancerCheatingViolationId,
            ProposalId = baseDto.ProposalId,
            FreelancerUserId = baseDto.FreelancerUserId,
            FreelancerName = baseDto.FreelancerName,
            FreelancerEmail = baseDto.FreelancerEmail,
            JobPostId = baseDto.JobPostId,
            JobTitle = baseDto.JobTitle,
            ViolationNumber = baseDto.ViolationNumber,
            TotalEventCount = baseDto.TotalEventCount,
            CopyCount = baseDto.CopyCount,
            PasteCount = baseDto.PasteCount,
            TabSwitchCount = baseDto.TabSwitchCount,
            ScreenshotAttemptCount = baseDto.ScreenshotAttemptCount,
            FocusLossCount = baseDto.FocusLossCount,
            FullscreenExitCount = baseDto.FullscreenExitCount,
            Action = baseDto.Action,
            EloDelta = baseDto.EloDelta,
            SuspendedUntil = baseDto.SuspendedUntil,
            IsReviewed = baseDto.IsReviewed,
            ReviewedByAdminId = baseDto.ReviewedByAdminId,
            ReviewedByAdminName = baseDto.ReviewedByAdminName,
            ReviewedAt = baseDto.ReviewedAt,
            AdminNote = baseDto.AdminNote,
            CreatedAt = baseDto.CreatedAt,
            Events = events
        };
    }
}
