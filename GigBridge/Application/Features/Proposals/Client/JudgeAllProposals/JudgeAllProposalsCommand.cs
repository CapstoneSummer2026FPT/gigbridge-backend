using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Client.JudgeAllProposals;

public class BatchJudgeResultDto
{
    public int ProcessedCount { get; set; }
    public int RemainingCount { get; set; }
    public bool IsCompleted { get; set; }
    public List<ProposalDto> ProcessedProposals { get; set; } = new();
}

public class JudgeAllProposalsCommand : IRequest<BatchJudgeResultDto>
{
    public Guid JobPostId { get; set; }
    public Guid UserId { get; set; }
    public int BatchSize { get; set; } = 10;
}

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

        // 3. Fetch proposals for this job post (Status != Draft) that have not been judged yet
        var unjudgedProposals = await _context.Set<Proposal>()
            .Include(p => p.FreelancerProfiles)
            .Include(p => p.JobPosts)
                .ThenInclude(j => j.JobPostSkills)
                    .ThenInclude(js => js.Skills)
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

        int processedCount = 0;

        // 4. Process proposals in the batch
        foreach (var proposal in unjudgedProposals)
        {
            var answers = await _context.Set<ProposalAnswer>()
                .AsNoTracking()
                .Include(pa => pa.JobPostQuestions)
                .Where(pa => pa.ProposalsId == proposal.ProposalsId)
                .ToListAsync(cancellationToken);

            if (!answers.Any() || answers.All(pa => string.IsNullOrWhiteSpace(pa.AnswerText)))
            {
                var emptyJudging = new ProposalAiJudging
                {
                    ProposalAiJudgingsId = Guid.NewGuid(),
                    ProposalId = proposal.ProposalsId,
                    Score = 0,
                    Summary = "No answers submitted to vetting questions.",
                    RecommendedHire = false,
                    TechnicalSkillsJson = "[]",
                    SoftSkillsJson = "[]",
                    HolisticAdjustment = 0,
                    HolisticAdjustmentReason = "No answers submitted.",
                    GradedQuestionsJson = "[]",
                    EvaluatedAt = DateTime.UtcNow
                };

                if (proposal.ProposalAiJudging != null)
                {
                    proposal.ProposalAiJudging.Score = 0;
                    proposal.ProposalAiJudging.Summary = "No answers submitted to vetting questions.";
                    proposal.ProposalAiJudging.RecommendedHire = false;
                    proposal.ProposalAiJudging.TechnicalSkillsJson = "[]";
                    proposal.ProposalAiJudging.SoftSkillsJson = "[]";
                    proposal.ProposalAiJudging.HolisticAdjustment = 0;
                    proposal.ProposalAiJudging.HolisticAdjustmentReason = "No answers submitted.";
                    proposal.ProposalAiJudging.GradedQuestionsJson = "[]";
                    proposal.ProposalAiJudging.EvaluatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.Set<ProposalAiJudging>().Add(emptyJudging);
                    proposal.ProposalAiJudging = emptyJudging;
                }

                processedCount++;
                continue;
            }

            var requestDto = new AnalyzeVettingRequestDto
            {
                FreelancerId = proposal.FreelancerProfiles.UserId.ToString(),
                JobTitle = proposal.JobPosts.Title,
                JobDescription = proposal.JobPosts.Description,
                JobSkills = proposal.JobPosts.JobPostSkills.Select(js => js.Skills.Name).ToList(),
                QaPairs = answers.Select(pa => new QuestionAnswerPairDto
                {
                    QuestionIndex = pa.JobPostQuestions.OrderIndex,
                    QuestionText = pa.JobPostQuestions.QuestionText,
                    CandidateAnswer = pa.AnswerText
                }).OrderBy(q => q.QuestionIndex).ToList()
            };

            try
            {
                var evalResult = await _aiServiceClient.AnalyzeVettingAsync(requestDto, cancellationToken);

                var techSkillsJson = System.Text.Json.JsonSerializer.Serialize(evalResult.TechnicalSkills ?? new List<string>());
                var softSkillsJson = System.Text.Json.JsonSerializer.Serialize(evalResult.SoftSkills ?? new List<string>());
                var gradedQuestionsJson = System.Text.Json.JsonSerializer.Serialize(evalResult.GradedQuestions ?? new List<GradedQuestionDto>());

                if (proposal.ProposalAiJudging != null)
                {
                    proposal.ProposalAiJudging.Score = evalResult.Score;
                    proposal.ProposalAiJudging.Summary = evalResult.Summary ?? string.Empty;
                    proposal.ProposalAiJudging.RecommendedHire = evalResult.RecommendedHire;
                    proposal.ProposalAiJudging.TechnicalSkillsJson = techSkillsJson;
                    proposal.ProposalAiJudging.SoftSkillsJson = softSkillsJson;
                    proposal.ProposalAiJudging.HolisticAdjustment = evalResult.HolisticAdjustment;
                    proposal.ProposalAiJudging.HolisticAdjustmentReason = evalResult.HolisticAdjustmentReason;
                    proposal.ProposalAiJudging.GradedQuestionsJson = gradedQuestionsJson;
                    proposal.ProposalAiJudging.EvaluatedAt = DateTime.UtcNow;
                }
                else
                {
                    var newJudging = new ProposalAiJudging
                    {
                        ProposalAiJudgingsId = Guid.NewGuid(),
                        ProposalId = proposal.ProposalsId,
                        Score = evalResult.Score,
                        Summary = evalResult.Summary ?? string.Empty,
                        RecommendedHire = evalResult.RecommendedHire,
                        TechnicalSkillsJson = techSkillsJson,
                        SoftSkillsJson = softSkillsJson,
                        HolisticAdjustment = evalResult.HolisticAdjustment,
                        HolisticAdjustmentReason = evalResult.HolisticAdjustmentReason,
                        GradedQuestionsJson = gradedQuestionsJson,
                        EvaluatedAt = DateTime.UtcNow
                    };

                    _context.Set<ProposalAiJudging>().Add(newJudging);
                    proposal.ProposalAiJudging = newJudging;
                }

                processedCount++;
            }
            catch
            {
                // If AI call fails for a proposal, log and continue batch
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

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
}
