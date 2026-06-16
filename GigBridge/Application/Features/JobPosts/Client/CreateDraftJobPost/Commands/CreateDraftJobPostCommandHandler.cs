using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.CreateDraftJobPost.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.CreateDraftJobPost.Commands;

public class CreateDraftJobPostCommandHandler
    : IRequestHandler<CreateDraftJobPostCommand, CreateDraftJobPostResponse>
{
    private const int DraftStatus = 0;
    private const int PublicVisibility = 0;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CreateDraftJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<CreateDraftJobPostResponse> Handle(
        CreateDraftJobPostCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfile.ClientProfilesId,
            Title = "Untitled Job Post",
            Description = string.Empty,
            Status = DraftStatus,
            Visibility = PublicVisibility,
            CreatedAt = _dateTimeService.UtcNow
        };

        _context.Set<JobPost>().Add(jobPost);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateDraftJobPostResponse(jobPost.JobPostsId, jobPost.Status);
    }
}
