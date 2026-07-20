using Application.Common.Models.Ai;
using MediatR;

namespace Application.Features.AiInterviews.Freelancer.Audio.Queries;

public sealed record GetAiInterviewQuestionAudioQuery(
    string SessionId,
    int QuestionIndex,
    string AudioAccessToken) : IRequest<AiInterviewQuestionAudioResponseDto>;

public sealed record StreamAiInterviewQuestionAudioQuery(
    string SessionId,
    int QuestionIndex,
    string AudioAccessToken) : IRequest<AiInterviewAudioStreamDto>;
