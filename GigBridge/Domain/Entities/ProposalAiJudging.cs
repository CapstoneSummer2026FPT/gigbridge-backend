using System;

namespace Domain.Entities;

public class ProposalAiJudging
{
    public Guid ProposalAiJudgingsId { get; set; }

    public Guid ProposalId { get; set; }

    public int Score { get; set; }

    public string Summary { get; set; } = string.Empty;

    public bool RecommendedHire { get; set; }

    public string? TechnicalSkillsJson { get; set; }

    public string? SoftSkillsJson { get; set; }

    public int HolisticAdjustment { get; set; }

    public string? HolisticAdjustmentReason { get; set; }

    public string? GradedQuestionsJson { get; set; }

    public double TechnicalQualityScore { get; set; }

    public double ValueScore { get; set; }

    public string? VerdictBadge { get; set; }

    public string? QualityBand { get; set; }

    public double SavingsRatioPercent { get; set; }

    public double ScopeCompletenessPercent { get; set; }

    public string? FullEvaluationJson { get; set; }

    public DateTime EvaluatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Proposal Proposal { get; set; } = null!;
}
