using Application.Common.Models.Ai;

namespace Application.Common.Interfaces.IService;

public interface IAiServiceClient
{
    Task<JobPostGenerationResponseDto> GenerateJobDescriptionAsync(JobPostGenerationRequestDto request, CancellationToken cancellationToken = default);
    Task<AiInterviewQuestionResponseDto> StartInterviewAsync(AiInterviewStartRequestDto request, CancellationToken cancellationToken = default);
    Task<AiInterviewDraftResponseDto> TranscribeInterviewAudioAsync(
        string sessionId,
        Stream audioStream,
        string fileName,
        string contentType,
        string language,
        CancellationToken cancellationToken = default);
    Task<AiInterviewQuestionResponseDto> ConfirmInterviewAnswerAsync(
        AiInterviewConfirmRequestDto request,
        CancellationToken cancellationToken = default);
    Task<AiInterviewQuestionAudioResponseDto> GetInterviewQuestionAudioAsync(
        string sessionId,
        int questionIndex,
        string audioAccessToken,
        CancellationToken cancellationToken = default);
    Task<AiInterviewAudioStreamDto> StreamInterviewQuestionAudioAsync(
        string sessionId,
        int questionIndex,
        string audioAccessToken,
        CancellationToken cancellationToken = default);
    Task<VettingEvaluationResponseDto> AnalyzeVettingAsync(
        AnalyzeVettingRequestDto request,
        CancellationToken cancellationToken = default);
}
