using System;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.JobPosts.Common.ContentModeration;
using Application.Features.Contracts.Common.Internal;
using Application.Features.JobPosts.Client.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;

public class UpdateStatusJobPostCommandHandler
    : IRequestHandler<UpdateStatusJobPostCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IContentModerationService _contentModerationService;

    public UpdateStatusJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IContentModerationService contentModerationService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _contentModerationService = contentModerationService;
    }

    public async Task<bool> Handle(
        UpdateStatusJobPostCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(
                profile => profile.UserId == command.UserId,
                cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPost = await _context.Set<JobPost>()
            .Include(item => item.JobPostMilestonePlans)
                .ThenInclude(item => item.WorkItems)
            .FirstOrDefaultAsync(
                jobPost =>
                    jobPost.JobPostsId == command.JobPostId &&
                    jobPost.ClientProfilesId == clientProfile.ClientProfilesId,
                cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist or you do not have permission to update it.");
        }

        if (jobPost.Visibility == 3)
        {
            throw new BadRequestException("This job post has been locked by an admin and cannot be updated.");
        }

        JobPostContentModerationGuard.EnsureAllowed(
            _contentModerationService,
            jobPost.Title,
            jobPost.Description);

        if (command.Request.Status == JobPostSetupPublishGuard.OpenStatus)
        {
            JobPostSetupPublishGuard.EnsureProjectRequestCanPublish(
                jobPost,
                DateOnly.FromDateTime(_dateTimeService.UtcNow));
            if (jobPost.JobPostMilestonePlans.Count > 0)
            {
                var total = jobPost.JobPostMilestonePlans.Sum(item => item.Amount);
                jobPost.BudgetMin = total;
                jobPost.BudgetMax = total;
            }
        }

        jobPost.Status = command.Request.Status;
        jobPost.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
