using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

// Mirrors TalentRerank* in TalentMatchingDtos.cs but with query/candidate roles
// reversed: the freelancer profile is the query, open job posts are the candidates.
// Used for the Freelancer "browse jobs" recommendation feature.

public sealed class JobRerankRequestDto
{
    [JsonPropertyName("freelancer")]
    public JobRerankFreelancerDto Freelancer { get; set; } = new();

    [JsonPropertyName("candidates")]
    public List<JobRerankCandidateDto> Candidates { get; set; } = new();

    [JsonPropertyName("top_k")]
    public int TopK { get; set; }

    [JsonPropertyName("algorithm_version")]
    public string AlgorithmVersion { get; set; } = "2.0";

    [JsonPropertyName("scoring_version")]
    public string ScoringVersion { get; set; } = "weighted-features-v1";
}

public sealed class JobRerankFreelancerDto
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
    public List<JobRerankVerifiedWorkDto> VerifiedWork { get; set; } = new();
}

public sealed class JobRerankVerifiedWorkDto
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

public sealed class JobRerankCandidateDto
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

public sealed class JobRerankResponseDto
{
    [JsonPropertyName("matches")]
    public List<JobRerankMatchDto> Matches { get; set; } = new();

    [JsonPropertyName("algorithm_version")]
    public string AlgorithmVersion { get; set; } = string.Empty;

    [JsonPropertyName("embedding_model")]
    public string EmbeddingModel { get; set; } = string.Empty;

    [JsonPropertyName("scoring_version")]
    public string ScoringVersion { get; set; } = string.Empty;
}

public sealed class JobRerankMatchDto
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("embedding_score")]
    public double EmbeddingScore { get; set; }

    [JsonPropertyName("algorithm_score")]
    public double AlgorithmScore { get; set; }

    [JsonPropertyName("match_reasons")]
    public List<string> MatchReasons { get; set; } = new();

    [JsonPropertyName("semantic_strengths")]
    public List<string> SemanticStrengths { get; set; } = new();
}
