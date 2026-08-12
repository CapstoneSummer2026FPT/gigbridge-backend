using Domain.Enums.AiInterviews;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;

using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.AiInterviews.Disable.Commands;

public sealed class DisableAiInterviewCommandHandler(
    IApplicationDbContext context) : IRequestHandler<DisableAiInterviewCommand, bool>
{
    public async Task<bool> Handle(
        DisableAiInterviewCommand command,
        CancellationToken cancellationToken)
    {
        var jobPost = await context.Set<JobPost>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobPostsId == command.JobPostId && x.Status == 1 &&
                x.ClientProfiles.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("Job post not found.");

        var activeDefinitions = await context.Set<AiInterviewDefinition>()
            .Where(x => x.JobPostId == command.JobPostId && x.Status != AiInterviewDefinitionStatus.Closed)
            .ToListAsync(cancellationToken);

        if (activeDefinitions.Count == 0)
        {
            return false;
        }

        foreach (var definition in activeDefinitions)
        {
            definition.Status = AiInterviewDefinitionStatus.Closed;
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
