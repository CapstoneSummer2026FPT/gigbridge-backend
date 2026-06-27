namespace Application.Features.Admin.Cheating.DTOs;

public class AdminCheatingViolationDto
{
    public Guid FreelancerCheatingViolationId { get; init; }
    public Guid ProposalId { get; init; }
    public Guid FreelancerUserId { get; init; }
    public string FreelancerName { get; init; } = string.Empty;
    public string FreelancerEmail { get; init; } = string.Empty;
    public Guid JobPostId { get; init; }
    public string JobTitle { get; init; } = string.Empty;
    public int ViolationNumber { get; init; }
    public int TotalEventCount { get; init; }
    public int CopyCount { get; init; }
    public int PasteCount { get; init; }
    public int TabSwitchCount { get; init; }
    public int ScreenshotAttemptCount { get; init; }
    public int FocusLossCount { get; init; }
    public int FullscreenExitCount { get; init; }
    public int Action { get; init; }
    public int EloDelta { get; init; }
    public DateTime? SuspendedUntil { get; init; }
    public bool IsReviewed { get; init; }
    public Guid? ReviewedByAdminId { get; init; }
    public string? ReviewedByAdminName { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? AdminNote { get; init; }
    public DateTime CreatedAt { get; init; }
}
