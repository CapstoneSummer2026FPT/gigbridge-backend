using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.UpdateVisibilityJobPost.Commands;

public class UpdateVisibilityJobPostCommandHandler
    : IRequestHandler<UpdateVisibilityJobPostCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateVisibilityJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(
        UpdateVisibilityJobPostCommand command,
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
            .FirstOrDefaultAsync(
                jobPost =>
                    jobPost.JobPostsId == command.JobPostId &&
                    jobPost.ClientProfilesId == clientProfile.ClientProfilesId,
                cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist or you do not have permission to update it.");
        }

        jobPost.Visibility = command.Request.Visibility;
        jobPost.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}