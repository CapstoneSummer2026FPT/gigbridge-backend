using Domain.Enums.AiInterviews;
using Domain.Enums.Proposals;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Ai;
using Application.Common.Interfaces.Time;
using Application.Common.Models.Ai;
using Application.Features.Premium.Client.SmartTalentMatching.Feedback;
using Domain.Entities;
using Domain.Enums.Premium;
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
            .Include(x => x.JobPostQuestions)
            .FirstOrDefaultAsync(x => x.JobPostsId == command.JobPostId && x.Status == 1,
                cancellationToken)
            ?? throw new NotFoundException("Job post not found.");

        if (jobPost.JobPostQuestions is null || !jobPost.JobPostQuestions.Any())
        {
            throw new BadRequestException("This job post does not have any predefined questions.");
        }
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

        if (definition is null)
            throw new BadRequestException("This job does not have an AI interview enabled.");
        AiInterviewDefinition activeDefinition = definition;

        var hasSubmittedProposal = await context.Set<Proposal>()
            .AsNoTracking()
            .AnyAsync(proposal => proposal.JobPostsId == command.JobPostId &&
                proposal.FreelancerProfiles.UserId == command.UserId &&
                proposal.ModerationStatus == (int)ProposalModerationStatus.Active &&
                proposal.Status == 0,
                cancellationToken);
        if (!hasSubmittedProposal)
            throw new ForbiddenAccessException(
                "Submit a proposal for this job before starting its AI interview.");

        var alreadyCompleted = await context.Set<AiInterviewAttempt>()
            .AsNoTracking()
            .AnyAsync(attempt => attempt.AiInterviewDefinitionId == activeDefinition.AiInterviewDefinitionsId &&
                attempt.FreelancerUserId == command.UserId &&
                attempt.Status == AiInterviewAttemptStatus.Completed,
                cancellationToken);
        if (alreadyCompleted)
            throw new ConflictException("This AI interview has already been completed.");

        var skills = jobPost.JobPostSkills.Where(x => x.Skills is not null)
            .Select(x => x.Skills.Name).Concat(jobPost.CustomSkillNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (string.IsNullOrWhiteSpace(activeDefinition.ExternalReference))
        {
            var registered = await aiServiceClient.CreateInterviewDefinitionAsync(
                new AiInterviewDefinitionRequestDto
                {
                    JobId = jobPost.JobPostsId.ToString(),
                    JobTitle = jobPost.Title,
                    JobDescription = jobPost.Description,
                    JobSkills = skills,
                    Mode = activeDefinition.Mode,
                    Language = activeDefinition.Language,
                    QuestionCount = activeDefinition.QuestionCount
                },
                cancellationToken);
            activeDefinition.ExternalReference = registered.DefinitionReference;
            activeDefinition.Mode = registered.Mode;
            activeDefinition.Language = registered.Language;
            activeDefinition.QuestionCount = registered.QuestionCount;
            activeDefinition.Status = AiInterviewDefinitionStatus.Active;
            activeDefinition.UpdatedAt = clock.UtcNow;
        }
        var jobQuestions = jobPost.JobPostQuestions
            .OrderBy(x => x.OrderIndex)
            .Select(x => x.QuestionText)
            .ToList();

        var result = await aiServiceClient.StartInterviewAsync(new AiInterviewStartRequestDto
        {
            JobId = jobPost.JobPostsId.ToString(),
            FreelancerId = command.UserId.ToString(),
            JobTitle = jobPost.Title,
            JobDescription = jobPost.Description,
            JobSkills = skills,
            Mode = activeDefinition.Mode,
            Language = activeDefinition.Language,
            QuestionCount = activeDefinition.QuestionCount,
            DefinitionReference = activeDefinition.ExternalReference,
            JobQuestions = jobQuestions
        }, cancellationToken);

        activeDefinition.Status = AiInterviewDefinitionStatus.Active;
        activeDefinition.UpdatedAt = clock.UtcNow;
        var attempt = new AiInterviewAttempt
        {
            AiInterviewAttemptsId = Guid.NewGuid(),
            AiInterviewDefinitionId = activeDefinition.AiInterviewDefinitionsId,
            FreelancerUserId = command.UserId,
            ExternalSessionId = result.SessionId,
            Status = AiInterviewAttemptStatus.InProgress,
            StartedAt = clock.UtcNow
        };
        context.Set<AiInterviewAttempt>().Add(attempt);
        var freelancerProfileId = await context.Set<FreelancerProfile>()
            .AsNoTracking()
            .Where(profile => profile.UserId == command.UserId)
            .Select(profile => (Guid?)profile.FreelancerProfilesId)
            .FirstOrDefaultAsync(cancellationToken);
        if (freelancerProfileId.HasValue)
        {
            await TalentMatchFeedbackWriter.TryAddLatestAttributedAsync(
                context,
                command.JobPostId,
                freelancerProfileId.Value,
                TalentMatchEventType.InterviewStarted,
                attempt.AiInterviewAttemptsId,
                clock.UtcNow,
                cancellationToken);
        }
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
        return result;
    }
}
