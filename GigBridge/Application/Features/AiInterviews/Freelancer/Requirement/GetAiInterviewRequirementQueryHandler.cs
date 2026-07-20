using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AiInterviews.Freelancer.Requirement;

public sealed class GetAiInterviewRequirementQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAiInterviewRequirementQuery, AiInterviewRequirementDto>
{
    public async Task<AiInterviewRequirementDto> Handle(
        GetAiInterviewRequirementQuery request,
        CancellationToken cancellationToken)
    {
        var definition = await context.Set<AiInterviewDefinition>().AsNoTracking()
            .Where(x => x.JobPostId == request.JobPostId &&
                x.Status != AiInterviewDefinitionStatus.Closed)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.AiInterviewDefinitionsId })
            .FirstOrDefaultAsync(cancellationToken);
        if (definition is null) return new(false, false, false, null);

        var attempts = context.Set<AiInterviewAttempt>().AsNoTracking().Where(x =>
            x.AiInterviewDefinitionId == definition.AiInterviewDefinitionsId &&
            x.FreelancerUserId == request.UserId);
        var completed = await attempts.AnyAsync(
            x => x.Status == AiInterviewAttemptStatus.Completed, cancellationToken);
        var inProgress = !completed && await attempts.AnyAsync(
            x => x.Status == AiInterviewAttemptStatus.InProgress, cancellationToken);
        return new(true, completed, inProgress, definition.AiInterviewDefinitionsId);
    }
}
