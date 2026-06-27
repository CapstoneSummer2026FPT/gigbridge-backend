namespace Application.Features.Proposals.Freelancer.Cheating.DTOs;

public record CheatingEventLogResponse(
    Guid ProposalId,
    int EventType,
    int TotalSessionEventCount,
    int CopyCount,
    int PasteCount,
    int TabSwitchCount,
    int ScreenshotAttemptCount,
    int FocusLossCount,
    int FullscreenExitCount,
    string WarningMessage);
