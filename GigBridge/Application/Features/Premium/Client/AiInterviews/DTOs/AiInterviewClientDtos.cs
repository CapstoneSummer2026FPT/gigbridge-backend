namespace Application.Features.Premium.Client.AiInterviews.DTOs;

public sealed record CreateAiInterviewRequest(
    string Language = "auto",
    string Mode = "voice",
    int QuestionCount = 5);

public sealed record AiInterviewDefinitionDto(
    Guid InterviewId,
    Guid JobPostId,
    string Language,
    string Mode,
    int QuestionCount,
    string Status,
    DateTime CreatedAt,
    string? ExternalReference);

public sealed record AiInterviewQuestionResultDto(
    int QuestionIndex,
    string? Question,
    string? Transcript,
    int? Score);

public sealed record AiInterviewAttemptResultDto(
    Guid AttemptId,
    string Status,
    int? OverallScore,
    int? CompatibilityScore,
    string? Summary,
    IReadOnlyList<string> TechnicalSkills,
    IReadOnlyList<string> SoftSkills,
    bool? RecommendedHire,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<AiInterviewQuestionResultDto> Questions);

public sealed record AiInterviewResultsDto(
    Guid InterviewId,
    Guid JobPostId,
    string Status,
    IReadOnlyList<AiInterviewAttemptResultDto> Attempts);
