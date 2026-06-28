using System;

namespace Domain.Entities;

public partial class FreelancerCheatingViolation
{
    public Guid FreelancerCheatingViolationsId { get; set; }

    public Guid ProposalsId { get; set; }

    public Guid FreelancerUserId { get; set; }

    public int ViolationNumber { get; set; }

    public int TotalEventCount { get; set; }

    public int CopyCount { get; set; }

    public int PasteCount { get; set; }

    public int TabSwitchCount { get; set; }

    public int ScreenshotAttemptCount { get; set; }

    public int FocusLossCount { get; set; }

    public int FullscreenExitCount { get; set; }

    public int Action { get; set; }

    public int EloDelta { get; set; }

    public DateTime? SuspendedUntil { get; set; }

    public bool IsReviewed { get; set; }

    public Guid? ReviewedByAdminId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User FreelancerUser { get; set; } = null!;

    public virtual Proposal Proposals { get; set; } = null!;

    public virtual User? ReviewedByAdmin { get; set; }
}
