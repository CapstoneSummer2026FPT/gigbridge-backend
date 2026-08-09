using Application.Common.Models.Ai;

namespace Application.Common.Interfaces.IService;

public interface IAiServiceClient
{
    Task<JobPostDetailsGenerationResponseDto> GenerateJobDescriptionDetailsAsync(JobPostGenerationRequestDto request, CancellationToken cancellationToken = default);
    Task<JobPostHiringPlanGenerationResponseDto> GenerateJobHiringPlanAsync(JobPostHiringPlanGenerationRequestDto request, CancellationToken cancellationToken = default);
    Task<AiInterviewDefinitionResponseDto> CreateInterviewDefinitionAsync(
        AiInterviewDefinitionRequestDto request,
        CancellationToken cancellationToken = default);
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
    Task<AiChatBoxResponseDto> QueryChatBoxAsync(
        AiChatBoxRequestDto request,
        CancellationToken cancellationToken = default);
    Task<TalentMatchingResponseDto> RecommendTalentAsync(
        TalentMatchingRequestDto request,
        CancellationToken cancellationToken = default);
    Task<TalentRerankResponseDto> RerankTalentAsync(
        TalentRerankRequestDto request,
        CancellationToken cancellationToken = default);
    Task<JobRerankResponseDto> RerankJobsForFreelancerAsync(
        JobRerankRequestDto request,
        CancellationToken cancellationToken = default);
}


