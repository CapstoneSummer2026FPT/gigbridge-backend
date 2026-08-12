using Domain.Enums.Disputes;

namespace Domain.Entities;

public partial class Dispute
{
    public bool IsVipPriority { get; set; }
    public DateTime? ResolutionTargetAt { get; set; }
    public DisputeAiAnalysisStatus AiAnalysisStatus { get; set; } = DisputeAiAnalysisStatus.Unavailable;
}
