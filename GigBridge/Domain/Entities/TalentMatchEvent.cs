namespace Domain.Entities;

public sealed class TalentMatchEvent
{
    public Guid TalentMatchEventId { get; set; }
    public Guid TalentMatchRunId { get; set; }
    public Guid FreelancerProfileId { get; set; }
    public int EventType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public TalentMatchRun TalentMatchRun { get; set; } = null!;
    public TalentMatchResult TalentMatchResult { get; set; } = null!;
    public FreelancerProfile FreelancerProfile { get; set; } = null!;
}
