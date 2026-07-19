using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AiInterviews.Freelancer.Transcribe.Commands;

public sealed class TranscribeAiInterviewCommandHandler(
    IApplicationDbContext context,
    IAiServiceClient aiServiceClient,
    IDateTimeService clock) : IRequestHandler<TranscribeAiInterviewCommand, AiInterviewDraftResponseDto>
{
    public async Task<AiInterviewDraftResponseDto> Handle(
        TranscribeAiInterviewCommand command,
        CancellationToken cancellationToken)
    {
        var result = await aiServiceClient.TranscribeInterviewAudioAsync(
            command.SessionId, command.AudioStream, command.FileName, command.ContentType,
            command.Language, cancellationToken);
        var attempt = await context.Set<AiInterviewAttempt>().FirstOrDefaultAsync(x =>
            x.ExternalSessionId == command.SessionId && x.FreelancerUserId == command.UserId,
            cancellationToken);
        if (attempt is null) return result;
        var answer = await context.Set<AiInterviewAnswerResult>().FirstOrDefaultAsync(x =>
            x.AiInterviewAttemptId == attempt.AiInterviewAttemptsId &&
            x.QuestionIndex == result.QuestionIndex, cancellationToken);
        if (answer is null)
            context.Set<AiInterviewAnswerResult>().Add(new AiInterviewAnswerResult
            {
                AiInterviewAnswerResultsId = Guid.NewGuid(),
                AiInterviewAttemptId = attempt.AiInterviewAttemptsId,
                QuestionIndex = result.QuestionIndex,
                Transcript = result.Transcript,
                CreatedAt = clock.UtcNow
            });
        else
        {
            answer.Transcript = result.Transcript;
            answer.UpdatedAt = clock.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken);
        return result;
    }
}
