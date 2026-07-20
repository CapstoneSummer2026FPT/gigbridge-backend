using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AiInterviews.Freelancer.Start.Commands;

public sealed class StartAiInterviewCommandHandler(
    IApplicationDbContext context,
    IAiServiceClient aiServiceClient,
    IDateTimeService clock) : IRequestHandler<StartAiInterviewCommand, AiInterviewQuestionResponseDto>
{
    public async Task<AiInterviewQuestionResponseDto> Handle(
        StartAiInterviewCommand command,
        CancellationToken cancellationToken)
    {
        var jobPost = await context.Set<JobPost>().AsNoTracking()
            .Include(x => x.JobPostSkills).ThenInclude(x => x.Skills)
            .FirstOrDefaultAsync(x => x.JobPostsId == command.JobPostId && x.Status == 1,
                cancellationToken)
            ?? throw new NotFoundException("Job post not found.");
        AiInterviewDefinition? definition = null;
        if (command.InterviewDefinitionId.HasValue)
            definition = await context.Set<AiInterviewDefinition>()
                .FirstOrDefaultAsync(x => x.AiInterviewDefinitionsId == command.InterviewDefinitionId &&
                    x.JobPostId == command.JobPostId && x.Status != AiInterviewDefinitionStatus.Closed,
                cancellationToken)
                ?? throw new NotFoundException("AI interview not found.");
        else
            definition = await context.Set<AiInterviewDefinition>()
                .Where(x => x.JobPostId == command.JobPostId &&
                    x.Status != AiInterviewDefinitionStatus.Closed)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        var skills = jobPost.JobPostSkills.Where(x => x.Skills is not null)
            .Select(x => x.Skills.Name).Concat(jobPost.CustomSkillNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (definition is not null && string.IsNullOrWhiteSpace(definition.ExternalReference))
        {
            var registered = await aiServiceClient.CreateInterviewDefinitionAsync(
                new AiInterviewDefinitionRequestDto
                {
                    JobId = jobPost.JobPostsId.ToString(),
                    JobTitle = jobPost.Title,
                    JobDescription = jobPost.Description,
                    JobSkills = skills,
                    Mode = definition.Mode,
                    Language = definition.Language,
                    QuestionCount = definition.QuestionCount
                },
                cancellationToken);
            definition.ExternalReference = registered.DefinitionReference;
            definition.Mode = registered.Mode;
            definition.Language = registered.Language;
            definition.QuestionCount = registered.QuestionCount;
            definition.Status = AiInterviewDefinitionStatus.Active;
            definition.UpdatedAt = clock.UtcNow;
        }
        var result = await aiServiceClient.StartInterviewAsync(new AiInterviewStartRequestDto
        {
            JobId = jobPost.JobPostsId.ToString(),
            FreelancerId = command.UserId.ToString(),
            JobTitle = jobPost.Title,
            JobDescription = jobPost.Description,
            JobSkills = skills,
            Mode = definition?.Mode ?? command.Mode,
            Language = definition?.Language ?? command.Language,
            QuestionCount = definition?.QuestionCount,
            DefinitionReference = definition?.ExternalReference
        }, cancellationToken);

        if (definition is not null)
        {
            definition.Status = AiInterviewDefinitionStatus.Active;
            definition.UpdatedAt = clock.UtcNow;
            var attempt = new AiInterviewAttempt
            {
                AiInterviewAttemptsId = Guid.NewGuid(),
                AiInterviewDefinitionId = definition.AiInterviewDefinitionsId,
                FreelancerUserId = command.UserId,
                ExternalSessionId = result.SessionId,
                Status = AiInterviewAttemptStatus.InProgress,
                StartedAt = clock.UtcNow
            };
            context.Set<AiInterviewAttempt>().Add(attempt);
            if (!string.IsNullOrWhiteSpace(result.QuestionText))
                context.Set<AiInterviewAnswerResult>().Add(new AiInterviewAnswerResult
                {
                    AiInterviewAnswerResultsId = Guid.NewGuid(),
                    AiInterviewAttemptId = attempt.AiInterviewAttemptsId,
                    QuestionIndex = result.QuestionIndex,
                    QuestionText = result.QuestionText,
                    CreatedAt = clock.UtcNow
                });
            await context.SaveChangesAsync(cancellationToken);
        }
        return result;
    }
}
