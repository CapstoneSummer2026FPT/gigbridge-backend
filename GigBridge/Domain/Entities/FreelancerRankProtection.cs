namespace Domain.Entities;

public sealed class FreelancerRankProtection
{
    public Guid FreelancerRankProtectionsId { get; set; }
    public Guid FreelancerProfileId { get; set; }
    public bool IsVacationModeEnabled { get; set; }
    public DateTime RankProtectionStartedAt { get; set; }
    public DateTime RankProtectionEndsAt { get; set; }
    public string? RankProtectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public FreelancerProfile FreelancerProfile { get; set; } = null!;
}
