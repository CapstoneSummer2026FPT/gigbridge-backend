using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using MediatR;

namespace Application.Features.AiInterviews.Freelancer.Audio.Queries;

public sealed class GetAiInterviewQuestionAudioQueryHandler(IAiServiceClient aiServiceClient)
    : IRequestHandler<GetAiInterviewQuestionAudioQuery, AiInterviewQuestionAudioResponseDto>,
      IRequestHandler<StreamAiInterviewQuestionAudioQuery, AiInterviewAudioStreamDto>
{
    public Task<AiInterviewQuestionAudioResponseDto> Handle(
        GetAiInterviewQuestionAudioQuery query,
        CancellationToken cancellationToken) => aiServiceClient.GetInterviewQuestionAudioAsync(
            query.SessionId, query.QuestionIndex, query.AudioAccessToken, cancellationToken);

    public Task<AiInterviewAudioStreamDto> Handle(
        StreamAiInterviewQuestionAudioQuery query,
        CancellationToken cancellationToken) => aiServiceClient.StreamInterviewQuestionAudioAsync(
            query.SessionId, query.QuestionIndex, query.AudioAccessToken, cancellationToken);
}
