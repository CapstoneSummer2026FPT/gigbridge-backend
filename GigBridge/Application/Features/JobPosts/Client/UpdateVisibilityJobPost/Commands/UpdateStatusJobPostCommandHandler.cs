using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;

public class UpdateStatusJobPostCommandHandler
    : IRequestHandler<UpdateStatusJobPostCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateStatusJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
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
            .FirstOrDefaultAsync(
                jobPost =>
                    jobPost.JobPostsId == command.JobPostId &&
                    jobPost.ClientProfilesId == clientProfile.ClientProfilesId,
                cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist or you do not have permission to update it.");
        }

        jobPost.Status = command.Request.Status;
        jobPost.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}