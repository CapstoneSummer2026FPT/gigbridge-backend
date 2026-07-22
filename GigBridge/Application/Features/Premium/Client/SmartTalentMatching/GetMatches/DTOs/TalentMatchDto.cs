namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;

public sealed record TalentMatchScoreBreakdownDto(
    decimal StructuredSkillFit,
    decimal SkillCoverage,
    decimal SkillExpertise,
    decimal VerifiedSkillEvidence,
    decimal TaxonomyFit,
    decimal Reliability,
    decimal BayesianRating,
    decimal CompletedContracts,
    decimal Availability,
    decimal ReputationAndProfile,
    decimal Elo,
    decimal ProfileCompleteness,
    decimal? AiSemantic = null);

public sealed record TalentMatchEligibilityEvidenceDto(
    decimal RequiredSkillCoverage,
    int RequiredSkillsMatched,
    int RequiredSkillsTotal,
    bool MeetsRequiredSkillThreshold,
    bool SingleRequiredSkillSatisfied);

public sealed record TalentMatchVerifiedWorkEvidenceDto(
    int CompletedContracts,
    int ContractsMatchingJobSkills,
    int ContractsMatchingCategory,
    IReadOnlyList<string> VerifiedSkills);

public sealed record TalentMatchDto(
    Guid FreelancerId,
    Guid UserId,
    string DisplayName,
    string? Title,
    decimal MatchPercentage,
    string Confidence,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<string> CustomSkillHints,
    IReadOnlyList<string> Reasons,
    TalentMatchScoreBreakdownDto ScoreBreakdown,
    TalentMatchEligibilityEvidenceDto EligibilityEvidence,
    TalentMatchVerifiedWorkEvidenceDto VerifiedWorkEvidence);

public sealed record TalentMatchingResultDto(
    Guid JobPostId,
    string Mode,
    bool IsPartial,
    IReadOnlyList<TalentMatchDto> Matches);

public sealed record TalentMatchingFiltersDto(
    int? Availability = null,
    int? MinimumProficiency = null,
    int? MinimumYears = null,
    double? MinimumRating = null,
    int? MinimumCompletedContracts = null,
    Guid? MajorCategoryId = null,
    Guid? MajorId = null,
    IReadOnlyList<Guid>? SkillIds = null);
