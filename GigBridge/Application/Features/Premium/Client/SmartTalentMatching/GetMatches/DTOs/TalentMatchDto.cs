namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;

public sealed record TalentMatchDto(
    Guid FreelancerId,
    string DisplayName,
    string? Title,
    decimal MatchPercentage,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<string> Reasons);

public sealed record TalentMatchingResultDto(
    Guid JobPostId,
    IReadOnlyList<TalentMatchDto> Matches);
