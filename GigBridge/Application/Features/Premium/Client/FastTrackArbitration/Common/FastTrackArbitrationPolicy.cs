using Domain.Enums.Disputes;

namespace Application.Features.Premium.Client.FastTrackArbitration.Common;

public sealed record FastTrackArbitrationDecision(
    bool IsVipPriority,
    DateTime? ResolutionTargetAt,
    DisputeAiAnalysisStatus AiAnalysisStatus);

public static class FastTrackArbitrationPolicy
{
    public static FastTrackArbitrationDecision Evaluate(bool isPremiumClient, DateTime utcNow) =>
        isPremiumClient
            ? new FastTrackArbitrationDecision(
                true,
                utcNow.AddHours(24),
                DisputeAiAnalysisStatus.Unavailable)
            : new FastTrackArbitrationDecision(
                false,
                null,
                DisputeAiAnalysisStatus.Unavailable);
}
