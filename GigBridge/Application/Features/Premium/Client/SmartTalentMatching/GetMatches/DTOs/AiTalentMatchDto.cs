namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;

public sealed record AiTalentMatchScoreBreakdownDto(
    decimal Embedding,
    decimal Algorithm,
    decimal Evidence);

public sealed record AiTalentMatchDto(
    Guid FreelancerProfileId,
    Guid UserId,
    string DisplayName,
    string? Title,
    string? AvatarUrl,
    string? Location,
    int Availability,
    int Rank,
    decimal FinalScore,
    string Confidence,
    AiTalentMatchScoreBreakdownDto ScoreBreakdown,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<string> SemanticStrengths,
    IReadOnlyList<string> Reasons,
    double AverageRating,
    int ReviewCount,
    int CompletedContracts,
    int EloPoints);

public sealed record AiTalentMatchingResultDto(
    Guid MatchRunId,
    Guid JobPostId,
    string Mode,
    string AlgorithmVersion,
    IReadOnlyList<AiTalentMatchDto> Matches);

public sealed record AiTalentMatchingFiltersDto(
    int? Availability = null,
    Guid? MajorCategoryId = null,
    IReadOnlyList<Guid>? SkillIds = null);

public sealed record AiTalentMatchingRequest(
    int TopK = 10,
    AiTalentMatchingFiltersDto? Filters = null);

public sealed record TalentMatchEventRequest(
    Guid MatchRunId,
    Guid FreelancerProfileId,
    string EventType,
    string IdempotencyKey);
