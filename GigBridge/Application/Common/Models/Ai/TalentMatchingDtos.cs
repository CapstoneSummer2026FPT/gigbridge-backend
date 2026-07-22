using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

public sealed class TalentMatchingRequestDto
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("top_k")]
    public int TopK { get; set; }
}

public sealed class TalentMatchingResponseDto
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("matches")]
    public List<TalentMatchResultDto> Matches { get; set; } = new();
}

public sealed class TalentMatchResultDto
{
    [JsonPropertyName("freelancer_id")]
    public string FreelancerId { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("match_score")]
    public double MatchScore { get; set; }

    [JsonPropertyName("match_reasons")]
    public List<string> MatchReasons { get; set; } = new();

    [JsonPropertyName("skills_matched")]
    public List<string> SkillsMatched { get; set; } = new();

    [JsonPropertyName("skills_missing")]
    public List<string> SkillsMissing { get; set; } = new();
}

public sealed class TalentRerankRequestDto
{
    [JsonPropertyName("job")]
    public TalentRerankJobDto Job { get; set; } = new();

    [JsonPropertyName("candidates")]
    public List<TalentRerankCandidateDto> Candidates { get; set; } = new();

    [JsonPropertyName("top_k")]
    public int TopK { get; set; }

    [JsonPropertyName("algorithm_version")]
    public string AlgorithmVersion { get; set; } = "2.0";

    [JsonPropertyName("scoring_version")]
    public string ScoringVersion { get; set; } = "weighted-features-v1";
}

public sealed class TalentRerankJobDto
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("industry")]
    public string? Industry { get; set; }

    [JsonPropertyName("major_id")]
    public string? MajorId { get; set; }

    [JsonPropertyName("major_name")]
    public string? MajorName { get; set; }

    [JsonPropertyName("major_category_id")]
    public string? MajorCategoryId { get; set; }

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = new();

    [JsonPropertyName("custom_skills")]
    public List<string> CustomSkills { get; set; } = new();

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("estimated_duration")]
    public string? EstimatedDuration { get; set; }
}

public sealed class TalentRerankCandidateDto
{
    [JsonPropertyName("freelancer_id")]
    public string FreelancerId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("availability")]
    public int Availability { get; set; }

    [JsonPropertyName("major_id")]
    public string? MajorId { get; set; }

    [JsonPropertyName("major_name")]
    public string? MajorName { get; set; }

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = new();

    [JsonPropertyName("verified_work")]
    public List<TalentRerankVerifiedWorkDto> VerifiedWork { get; set; } = new();
}

public sealed class TalentRerankVerifiedWorkDto
{
    [JsonPropertyName("contract_id")]
    public string ContractId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("major_name")]
    public string? MajorName { get; set; }

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = new();
}

public sealed class TalentRerankResponseDto
{
    [JsonPropertyName("matches")]
    public List<TalentRerankMatchDto> Matches { get; set; } = new();

    [JsonPropertyName("algorithm_version")]
    public string AlgorithmVersion { get; set; } = string.Empty;

    [JsonPropertyName("embedding_model")]
    public string EmbeddingModel { get; set; } = string.Empty;

    [JsonPropertyName("scoring_version")]
    public string ScoringVersion { get; set; } = string.Empty;
}

public sealed class TalentRerankMatchDto
{
    [JsonPropertyName("freelancer_id")]
    public string FreelancerId { get; set; } = string.Empty;

    [JsonPropertyName("embedding_score")]
    public double EmbeddingScore { get; set; }

    [JsonPropertyName("algorithm_score")]
    public double AlgorithmScore { get; set; }

    [JsonPropertyName("match_reasons")]
    public List<string> MatchReasons { get; set; } = new();

    [JsonPropertyName("semantic_strengths")]
    public List<string> SemanticStrengths { get; set; } = new();
}
