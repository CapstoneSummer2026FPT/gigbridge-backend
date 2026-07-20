using Domain.Enums;

namespace Domain.Entities;

public partial class Dispute
{
    public bool IsVipPriority { get; set; }
    public DateTime? ResolutionTargetAt { get; set; }
    public DisputeAiAnalysisStatus AiAnalysisStatus { get; set; } = DisputeAiAnalysisStatus.Unavailable;
    public string? AiSuggestedResolution { get; set; }
}
