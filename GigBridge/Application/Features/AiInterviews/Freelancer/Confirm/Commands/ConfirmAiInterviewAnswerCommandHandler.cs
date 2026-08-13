using Domain.Enums.AiInterviews;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Ai;
using Application.Common.Interfaces.Time;
using Application.Common.Models.Ai;
using Application.Features.Premium.Client.SmartTalentMatching.Feedback;
using Domain.Entities;
using Domain.Enums.Premium;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AiInterviews.Freelancer.Confirm.Commands;

public sealed class ConfirmAiInterviewAnswerCommandHandler(
    IApplicationDbContext context,
    IAiServiceClient aiServiceClient,
    IDateTimeService clock) : IRequestHandler<ConfirmAiInterviewAnswerCommand, AiInterviewQuestionResponseDto>
{
    public async Task<AiInterviewQuestionResponseDto> Handle(
        ConfirmAiInterviewAnswerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await aiServiceClient.ConfirmInterviewAnswerAsync(new AiInterviewConfirmRequestDto
        {
            SessionId = command.SessionId,
            CorrectedText = command.CorrectedText
        }, cancellationToken);
        var attempt = await context.Set<AiInterviewAttempt>().FirstOrDefaultAsync(x =>
            x.ExternalSessionId == command.SessionId && x.FreelancerUserId == command.UserId,
            cancellationToken);
        if (attempt is null) return result;
        if (result.IsCompleted && result.Feedback is not null)
        {
            attempt.Status = AiInterviewAttemptStatus.Completed;
            attempt.OverallScore = result.Feedback.Score;
            attempt.CompatibilityScore = result.Feedback.Score;
            attempt.EvaluationSummary = result.Feedback.Summary;
            attempt.TechnicalSkillsJson = JsonSerializer.Serialize(result.Feedback.TechnicalSkills);
            attempt.SoftSkillsJson = JsonSerializer.Serialize(result.Feedback.SoftSkills);
            attempt.RecommendedHire = result.Feedback.RecommendedHire;
            attempt.CompletedAt = clock.UtcNow;
            var jobPostId = await context.Set<AiInterviewDefinition>()
                .AsNoTracking()
                .Where(item => item.AiInterviewDefinitionsId == attempt.AiInterviewDefinitionId)
                .Select(item => (Guid?)item.JobPostId)
                .FirstOrDefaultAsync(cancellationToken);
            var freelancerProfileId = await context.Set<FreelancerProfile>()
                .AsNoTracking()
                .Where(profile => profile.UserId == command.UserId)
                .Select(profile => (Guid?)profile.FreelancerProfilesId)
                .FirstOrDefaultAsync(cancellationToken);
            if (jobPostId.HasValue && freelancerProfileId.HasValue)
            {
                await TalentMatchFeedbackWriter.TryAddLatestAttributedAsync(
                    context,
                    jobPostId.Value,
                    freelancerProfileId.Value,
                    TalentMatchEventType.InterviewCompleted,
                    attempt.AiInterviewAttemptsId,
                    clock.UtcNow,
                    cancellationToken);
            }
        }
        else if (!string.IsNullOrWhiteSpace(result.QuestionText))
        {
            var exists = await context.Set<AiInterviewAnswerResult>().AnyAsync(x =>
                x.AiInterviewAttemptId == attempt.AiInterviewAttemptsId &&
                x.QuestionIndex == result.QuestionIndex, cancellationToken);
            if (!exists) context.Set<AiInterviewAnswerResult>().Add(new AiInterviewAnswerResult
            {
                AiInterviewAnswerResultsId = Guid.NewGuid(),
                AiInterviewAttemptId = attempt.AiInterviewAttemptsId,
                QuestionIndex = result.QuestionIndex,
                QuestionText = result.QuestionText,
                CreatedAt = clock.UtcNow
            });
        }
        await context.SaveChangesAsync(cancellationToken);
        return result;
    }
}
