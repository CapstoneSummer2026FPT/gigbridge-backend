using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Ai;
using Application.Common.Models.Ai;
using Application.Features.Proposals.Client.JudgeAllProposals.DTOs;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Client.JudgeAllProposals;

public class JudgeAllProposalsCommandHandler : IRequestHandler<JudgeAllProposalsCommand, BatchJudgeResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAiServiceClient _aiServiceClient;

    public JudgeAllProposalsCommandHandler(IApplicationDbContext context, IAiServiceClient aiServiceClient)
    {
        _context = context;
        _aiServiceClient = aiServiceClient;
    }

    public async Task<BatchJudgeResultDto> Handle(JudgeAllProposalsCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Client profile
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.UserId == request.UserId, cancellationToken);

        if (clientProfile == null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        // 2. Verify Job Post ownership
        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JobPostsId == request.JobPostId, cancellationToken);

        if (jobPost == null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        if (jobPost.ClientProfilesId != clientProfile.ClientProfilesId)
        {
            throw new ForbiddenAccessException("You do not have permission to judge proposals for this job.");
        }

        int maxBatch = request.BatchSize <= 0 || request.BatchSize > 20 ? 10 : request.BatchSize;

        // 3. Fetch JobPost details including original milestones and questions
        var jobPostDetails = await _context.Set<JobPost>()
            .AsNoTracking()
            .Include(j => j.JobPostSkills).ThenInclude(js => js.Skills)
            .Include(j => j.JobPostMilestonePlans)
            .Include(j => j.JobPostQuestions)
            .FirstOrDefaultAsync(j => j.JobPostsId == request.JobPostId, cancellationToken);

        var baselineDto = BuildJobBaselineInput(request.JobPostId, jobPostDetails);

        // 4. Fetch proposals for this job post (Status != Draft) that have not been judged yet
        var unjudgedProposals = await _context.Set<Proposal>()
            .Include(p => p.FreelancerProfiles)
                .ThenInclude(f => f.User)
            .Include(p => p.JobPosts)
                .ThenInclude(j => j.JobPostSkills)
                    .ThenInclude(js => js.Skills)
            .Include(p => p.ProposalMilestonePlans)
            .Include(p => p.ProposalAnswers)
                .ThenInclude(pa => pa.JobPostQuestions)
            .Include(p => p.ProposalAiJudging)
            .Where(p => p.JobPostsId == request.JobPostId && p.Status != 0 && p.ProposalAiJudging == null)
            .OrderBy(p => p.SubmittedAt)
            .Take(maxBatch)
            .ToListAsync(cancellationToken);

        if (!unjudgedProposals.Any())
        {
            return new BatchJudgeResultDto
            {
                ProcessedCount = 0,
                RemainingCount = 0,
                IsCompleted = true
            };
        }

        // 5. Build proposal offer DTOs for batch evaluation
        var proposalOfferDtos = BuildProposalOfferInputs(unjudgedProposals);

        var batchRequest = new BatchCandidateJudgingRequestDto
        {
            JobPostBaseline = baselineDto,
            Proposals = proposalOfferDtos,
            BatchChunkSize = 2
        };

        int processedCount = 0;

        try
        {
            var batchResponse = await _aiServiceClient.EvaluateCandidateBatchAsync(batchRequest, cancellationToken);
            var responseMap = batchResponse.JudgedProposals.ToDictionary(j => j.ProposalId);

            foreach (var proposal in unjudgedProposals)
            {
                var pIdStr = proposal.ProposalsId.ToString();
                if (responseMap.TryGetValue(pIdStr, out var evalResult))
                {
                    ApplyEvaluationToProposal(proposal, evalResult);
                    processedCount++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // If batch evaluation fails, return current status
        }

        var totalRemaining = await _context.Set<Proposal>()
            .CountAsync(p => p.JobPostsId == request.JobPostId && p.Status != 0 && p.ProposalAiJudging == null, cancellationToken);

        return new BatchJudgeResultDto
        {
            ProcessedCount = processedCount,
            RemainingCount = totalRemaining,
            IsCompleted = totalRemaining == 0,
            ProcessedProposals = ProposalProjection.ToDtos(unjudgedProposals)
        };
    }

    private static JobPostBaselineInputDto BuildJobBaselineInput(Guid jobPostId, JobPost? jobPostDetails)
    {
        var milestones = jobPostDetails?.JobPostMilestonePlans?.Select(m => new JobPostMilestoneInputDto
        {
            OrderIndex = m.OrderIndex,
            Title = m.Title,
            Description = m.Description,
            Amount = (double)m.Amount,
            EstimatedDuration = m.EstimatedDuration,
            Deliverables = m.Deliverables
        }).OrderBy(m => m.OrderIndex).ToList() ?? new List<JobPostMilestoneInputDto>();

        // Canonical BudgetMax: Use BudgetMax, or sum of baseline milestones, or BudgetMin
        double? canonicalBudgetMax = (double?)(jobPostDetails?.BudgetMax);
        if ((canonicalBudgetMax == null || canonicalBudgetMax <= 0) && milestones.Any(m => m.Amount > 0))
        {
            canonicalBudgetMax = milestones.Sum(m => m.Amount);
        }
        if (canonicalBudgetMax == null || canonicalBudgetMax <= 0)
        {
            canonicalBudgetMax = (double?)(jobPostDetails?.BudgetMin);
        }

        // Canonical EstimatedDuration: Use EstimatedDuration, or sum of baseline milestone durations
        string? canonicalDuration = jobPostDetails?.EstimatedDuration;
        if (string.IsNullOrWhiteSpace(canonicalDuration) || canonicalDuration == "—" || canonicalDuration == "null")
        {
            var validDurations = milestones
                .Select(m => m.EstimatedDuration)
                .Where(d => !string.IsNullOrWhiteSpace(d) && d != "—" && d != "null")
                .ToList();
            if (validDurations.Any())
            {
                canonicalDuration = string.Join(" + ", validDurations);
            }
        }

        return new JobPostBaselineInputDto
        {
            JobId = jobPostDetails?.JobPostsId.ToString() ?? jobPostId.ToString(),
            JobTitle = jobPostDetails?.Title ?? string.Empty,
            JobDescription = jobPostDetails?.Description ?? string.Empty,
            RequiredSkills = jobPostDetails?.JobPostSkills.Select(js => js.Skills.Name).ToList() ?? new List<string>(),
            BudgetMin = (double?)(jobPostDetails?.BudgetMin),
            BudgetMax = canonicalBudgetMax,
            EstimatedDuration = canonicalDuration,
            OriginalMilestones = milestones,
            VettingQuestions = jobPostDetails?.JobPostQuestions.Select(q => q.QuestionText).ToList() ?? new List<string>()
        };
    }

    private static List<ProposalOfferInputDto> BuildProposalOfferInputs(List<Proposal> unjudgedProposals)
    {
        return unjudgedProposals.Select(proposal => new ProposalOfferInputDto
        {
            ProposalId = proposal.ProposalsId.ToString(),
            FreelancerId = proposal.FreelancerProfiles.UserId.ToString(),
            FreelancerName = proposal.FreelancerProfiles.User?.FullName ?? "Freelancer",
            ProposedBudget = (double)(proposal.ProposedBudget ?? 0m),
            ProposedDuration = proposal.ProposedDuration,
            CoverLetter = proposal.CoverLetter,
            AnalysisSummary = proposal.AnalysisSummary,
            SolutionApproach = proposal.SolutionApproach,
            EditedMilestones = proposal.ProposalMilestonePlans.Select(m => new ProposalMilestoneInputDto
            {
                OrderIndex = m.OrderIndex,
                Title = m.Title,
                Description = m.Description,
                Amount = (double)m.Amount,
                EstimatedDuration = m.EstimatedDuration,
                Deliverables = m.Deliverables
            }).OrderBy(m => m.OrderIndex).ToList(),
            VettingQaAnswers = proposal.ProposalAnswers
                .OrderBy(pa => pa.JobPostQuestions != null ? pa.JobPostQuestions.OrderIndex : 0)
                .Select((pa, index) => new QuestionAnswerPairInputDto
                {
                    QuestionIndex = index + 1,
                    QuestionText = pa.JobPostQuestions?.QuestionText ?? string.Empty,
                    CandidateAnswer = pa.AnswerText
                }).ToList()
        }).ToList();
    }

    private void ApplyEvaluationToProposal(Proposal proposal, CandidateJudgingResponseDto evalResult)
    {
        var calc = evalResult.DeterministicCalculations;
        var fullJson = JsonSerializer.Serialize(evalResult);

        int scoreInt = (int)Math.Round(calc.OverallTechnicalQualityTQ);
        bool recommended = calc.VerdictBadge != "high_risk";
        string summaryText = $"Technical Quality: {calc.OverallTechnicalQualityTQ:F1} ({calc.QualityInterpretationBand}) | Value Score: {calc.FinalValueScoreVS:F1} | Badge: {calc.VerdictBadge}";

        var gradedQuestionsList = evalResult.LlmQualitativeEvaluation?.ScreeningQa?.Select(qa =>
        {
            double qScore = Math.Round(
                (qa.AnswerCorrectness?.Score ?? 0) * 0.40 +
                (qa.TechnicalReasoning?.Score ?? 0) * 0.25 +
                (qa.Relevance?.Score ?? 0) * 0.15 +
                (qa.Depth?.Score ?? 0) * 0.10 +
                (qa.PracticalExamples?.Score ?? 0) * 0.10
            );

            var feedbackList = new List<string>();
            if (qa.AnswerCorrectness?.Evidence?.Count > 0)
                feedbackList.Add("Accuracy: " + string.Join("; ", qa.AnswerCorrectness.Evidence.Select(e => e.Assessment)));
            if (qa.TechnicalReasoning?.Evidence?.Count > 0)
                feedbackList.Add("Reasoning: " + string.Join("; ", qa.TechnicalReasoning.Evidence.Select(e => e.Assessment)));

            return new
            {
                questionIndex = qa.QuestionIndex,
                questionText = qa.QuestionText,
                candidateAnswer = qa.CandidateAnswer,
                score = (int)qScore,
                feedback = feedbackList.Count > 0 ? string.Join(" | ", feedbackList) : "Đánh giá chi tiết dựa trên mức độ chính xác và lập luận kỹ thuật."
            };
        }).ToList();

        string gradedQuestionsJson = JsonSerializer.Serialize(gradedQuestionsList);

        if (proposal.ProposalAiJudging != null)
        {
            proposal.ProposalAiJudging.Score = scoreInt;
            proposal.ProposalAiJudging.Summary = summaryText;
            proposal.ProposalAiJudging.RecommendedHire = recommended;
            proposal.ProposalAiJudging.TechnicalQualityScore = calc.OverallTechnicalQualityTQ;
            proposal.ProposalAiJudging.ValueScore = calc.FinalValueScoreVS;
            proposal.ProposalAiJudging.VerdictBadge = calc.VerdictBadge;
            proposal.ProposalAiJudging.QualityBand = calc.QualityInterpretationBand;
            proposal.ProposalAiJudging.SavingsRatioPercent = calc.SavingsRatioPercent;
            proposal.ProposalAiJudging.ScopeCompletenessPercent = calc.ScopeCompletenessPercent;
            proposal.ProposalAiJudging.GradedQuestionsJson = gradedQuestionsJson;
            proposal.ProposalAiJudging.FullEvaluationJson = fullJson;
            proposal.ProposalAiJudging.EvaluatedAt = DateTime.UtcNow;
        }
        else
        {
            var newJudging = new ProposalAiJudging
            {
                ProposalAiJudgingsId = Guid.NewGuid(),
                ProposalId = proposal.ProposalsId,
                Score = scoreInt,
                Summary = summaryText,
                RecommendedHire = recommended,
                TechnicalQualityScore = calc.OverallTechnicalQualityTQ,
                ValueScore = calc.FinalValueScoreVS,
                VerdictBadge = calc.VerdictBadge,
                QualityBand = calc.QualityInterpretationBand,
                SavingsRatioPercent = calc.SavingsRatioPercent,
                ScopeCompletenessPercent = calc.ScopeCompletenessPercent,
                GradedQuestionsJson = gradedQuestionsJson,
                FullEvaluationJson = fullJson,
                EvaluatedAt = DateTime.UtcNow
            };

            _context.Set<ProposalAiJudging>().Add(newJudging);
            proposal.ProposalAiJudging = newJudging;
        }
    }
}
