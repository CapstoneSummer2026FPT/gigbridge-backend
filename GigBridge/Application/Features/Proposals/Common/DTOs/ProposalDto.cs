using System;
using System.Collections.Generic;

namespace Application.Features.Proposals.Common.DTOs;

public class ProposalDto
{
    public Guid ProposalsId { get; set; }
    public Guid JobPostsId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid FreelancerProfilesId { get; set; }
    public string FreelancerName { get; set; } = string.Empty;
    public string CoverLetter { get; set; } = string.Empty;
    public decimal ProposedBudget { get; set; }
    public string ProposedDuration { get; set; } = string.Empty;
    public int Status { get; set; }
    public int ModerationStatus { get; set; }
    public string? InvalidationReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string AnalysisSummaryPreview { get; set; } = string.Empty;
    public int WorkItemCount { get; set; }
    public int MilestoneCount { get; set; }
    public decimal MilestoneTotal { get; set; }
    public decimal? FirstMilestoneAmount { get; set; }
    public bool HasAiInterview { get; set; }
    public bool AiInterviewCompleted { get; set; }
    public bool AiInterviewInProgress { get; set; }
    public Guid? AiInterviewDefinitionId { get; set; }

    // AI Judging Metrics
    public int? AiScore { get; set; }
    public string? AiSummary { get; set; }
    public bool? AiRecommendedHire { get; set; }
    public DateTime? AiEvaluatedAt { get; set; }
    public List<string>? AiTechnicalSkills { get; set; }
    public List<string>? AiSoftSkills { get; set; }
}
