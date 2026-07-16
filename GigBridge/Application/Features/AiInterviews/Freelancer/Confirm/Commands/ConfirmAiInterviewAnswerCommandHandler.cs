using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Domain.Entities;
using Domain.Enums;
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
