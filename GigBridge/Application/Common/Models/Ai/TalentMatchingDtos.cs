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

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = new();
}

public sealed class TalentRerankCandidateDto
{
    [JsonPropertyName("freelancer_id")]
    public string FreelancerId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = new();

    [JsonPropertyName("work_history")]
    public List<string> WorkHistory { get; set; } = new();

    [JsonPropertyName("deterministic_score")]
    public decimal DeterministicScore { get; set; }
}

public sealed class TalentRerankResponseDto
{
    [JsonPropertyName("matches")]
    public List<TalentRerankMatchDto> Matches { get; set; } = new();
}

public sealed class TalentRerankMatchDto
{
    [JsonPropertyName("freelancer_id")]
    public string FreelancerId { get; set; } = string.Empty;

    [JsonPropertyName("semantic_score")]
    public double SemanticScore { get; set; }

    [JsonPropertyName("match_reasons")]
    public List<string> MatchReasons { get; set; } = new();

    [JsonPropertyName("skills_matched")]
    public List<string> SkillsMatched { get; set; } = new();

    [JsonPropertyName("skills_missing")]
    public List<string> SkillsMissing { get; set; } = new();
}
