namespace Domain.Entities;

public sealed class TalentMatchRun
{
    public Guid TalentMatchRunId { get; set; }
    public Guid ClientUserId { get; set; }
    public Guid JobPostId { get; set; }
    public string AlgorithmVersion { get; set; } = null!;
    public string? EmbeddingModel { get; set; }
    public string? ScoringVersion { get; set; }
    public int EligibleCandidateCount { get; set; }
    public int ReturnedCandidateCount { get; set; }
    public long LatencyMilliseconds { get; set; }
    public int Status { get; set; }
    public string? FailureCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public User ClientUser { get; set; } = null!;
    public JobPost JobPost { get; set; } = null!;
    public ICollection<TalentMatchResult> Results { get; set; } = new List<TalentMatchResult>();
    public ICollection<TalentMatchEvent> Events { get; set; } = new List<TalentMatchEvent>();
}
