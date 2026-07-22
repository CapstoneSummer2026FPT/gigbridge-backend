namespace Domain.Entities;

public sealed class TalentMatchResult
{
    public Guid TalentMatchResultId { get; set; }
    public Guid TalentMatchRunId { get; set; }
    public Guid FreelancerProfileId { get; set; }
    public int Rank { get; set; }
    public decimal EmbeddingScore { get; set; }
    public decimal AlgorithmScore { get; set; }
    public decimal EvidenceScore { get; set; }
    public decimal FinalScore { get; set; }
    public string Confidence { get; set; } = null!;
    public string[] MatchedSkills { get; set; } = [];
    public string[] MissingSkills { get; set; } = [];
    public string[] SemanticStrengths { get; set; } = [];
    public string[] Reasons { get; set; } = [];
    public DateTime CreatedAt { get; set; }

    public TalentMatchRun TalentMatchRun { get; set; } = null!;
    public FreelancerProfile FreelancerProfile { get; set; } = null!;
    public ICollection<TalentMatchEvent> Events { get; set; } = new List<TalentMatchEvent>();
}
