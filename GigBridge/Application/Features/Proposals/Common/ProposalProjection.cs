using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Application.Common.Models.Ai;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;

namespace Application.Features.Proposals.Common;

internal static class ProposalProjection
{
    public static List<ProposalDto> ToDtos(IEnumerable<Proposal> proposals)
    {
        return proposals.Select(ToDto).ToList();
    }

    private static ProposalDto ToDto(Proposal proposal)
    {
        var judging = proposal.ProposalAiJudging;
        List<string>? techSkills = null;
        List<string>? softSkills = null;

        if (!string.IsNullOrEmpty(judging?.TechnicalSkillsJson))
        {
            try { techSkills = JsonSerializer.Deserialize<List<string>>(judging.TechnicalSkillsJson); } catch { }
        }

        if (!string.IsNullOrEmpty(judging?.SoftSkillsJson))
        {
            try { softSkills = JsonSerializer.Deserialize<List<string>>(judging.SoftSkillsJson); } catch { }
        }

        List<GradedQuestionDto>? gradedQuestions = null;
        if (!string.IsNullOrEmpty(judging?.GradedQuestionsJson))
        {
            try { gradedQuestions = JsonSerializer.Deserialize<List<GradedQuestionDto>>(judging.GradedQuestionsJson); } catch { }
        }

        return new ProposalDto
        {
            ProposalsId = proposal.ProposalsId,
            JobPostsId = proposal.JobPostsId,
            JobTitle = proposal.JobPosts?.Title ?? string.Empty,
            FreelancerProfilesId = proposal.FreelancerProfilesId,
            FreelancerName = proposal.FreelancerProfiles?.User?.FullName ?? string.Empty,
            FreelancerUserId = proposal.FreelancerProfiles?.User?.UserId,
            CoverLetter = proposal.CoverLetter ?? string.Empty,
            ProposedBudget = proposal.ProposedBudget ?? 0m,
            ProposedDuration = proposal.ProposedDuration ?? string.Empty,
            Status = proposal.Status,
            ModerationStatus = proposal.ModerationStatus,
            InvalidationReason = proposal.InvalidationReason,
            SubmittedAt = proposal.SubmittedAt ?? DateTime.MinValue,
            ReviewedAt = proposal.UpdatedAt,
            AnalysisSummaryPreview = CreatePreview(proposal.AnalysisSummary),
            WorkItemCount = proposal.ProposalWorkBreakdownItems.Count,
            MilestoneCount = proposal.ProposalMilestonePlans.Count,
            MilestoneTotal = proposal.ProposalMilestonePlans.Sum(item => item.Amount),
            FirstMilestoneAmount = proposal.ProposalMilestonePlans
                .OrderBy(item => item.OrderIndex)
                .Select(item => (decimal?)item.Amount)
                .FirstOrDefault(),

            // AI Judging Fields
            AiScore = judging?.Score,
            AiTechnicalQualityScore = judging?.TechnicalQualityScore,
            AiValueScore = judging?.ValueScore,
            AiVerdictBadge = judging?.VerdictBadge,
            AiQualityBand = judging?.QualityBand,
            AiSavingsRatioPercent = judging?.SavingsRatioPercent,
            AiScopeCompletenessPercent = judging?.ScopeCompletenessPercent,
            AiFullEvaluationJson = judging?.FullEvaluationJson,
            AiSummary = judging?.Summary,
            AiRecommendedHire = judging?.RecommendedHire,
            AiEvaluatedAt = judging?.EvaluatedAt,
            AiTechnicalSkills = techSkills,
            AiSoftSkills = softSkills,
            AiHolisticAdjustmentReason = judging?.HolisticAdjustmentReason,
            AiHolisticAdjustment = judging?.HolisticAdjustment,
            AiIsAiGenerated = gradedQuestions?.Any(q => q.IsAiGenerated || q.AiConfidenceScore >= 0.5),
            AiConfidenceScore = gradedQuestions?.Any() == true ? gradedQuestions.Max(q => q.AiConfidenceScore) : null,
            AiDetectionSummary = gradedQuestions?.Any(q => q.IsAiGenerated || q.AiConfidenceScore >= 0.5) == true
                ? "At least 1 candidate answer exhibits AI generation patterns or generic textbook structure."
                : (gradedQuestions?.Any() == true ? "Candidate answers appear authentic with human experience indicators." : null),
            AiGradedQuestions = gradedQuestions
        };
    }

    private static string CreatePreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= 180 ? trimmed : $"{trimmed[..177]}...";
    }
}
