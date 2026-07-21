using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Client.EvaluateProposalVetting;

public class EvaluateProposalVettingCommand : IRequest<VettingEvaluationResponseDto>
{
    public Guid ProposalId { get; set; }
    public Guid UserId { get; set; } // Recruiter User ID
}

public class EvaluateProposalVettingCommandHandler : IRequestHandler<EvaluateProposalVettingCommand, VettingEvaluationResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAiServiceClient _aiServiceClient;

    public EvaluateProposalVettingCommandHandler(IApplicationDbContext context, IAiServiceClient aiServiceClient)
    {
        _context = context;
        _aiServiceClient = aiServiceClient;
    }

    public async Task<VettingEvaluationResponseDto> Handle(EvaluateProposalVettingCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Client profile exists
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.UserId == request.UserId, cancellationToken);

        if (clientProfile == null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        // 2. Fetch the proposal with JobPost and freelancer details
        var proposal = await _context.Set<Proposal>()
            .AsNoTracking()
            .Include(p => p.FreelancerProfiles)
            .Include(p => p.JobPosts)
                .ThenInclude(j => j.JobPostSkills)
                    .ThenInclude(js => js.Skills)
            .FirstOrDefaultAsync(p => p.ProposalsId == request.ProposalId, cancellationToken);

        if (proposal == null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        // 3. Verify recruiter owns the job post
        if (proposal.JobPosts.ClientProfilesId != clientProfile.ClientProfilesId)
        {
            throw new ForbiddenAccessException("You do not have permission to evaluate vetting for this proposal.");
        }

        // 4. Check if evaluation is already cached in database
        var existingJudging = await _context.Set<ProposalAiJudging>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.ProposalId == request.ProposalId, cancellationToken);

        if (existingJudging != null)
        {
            var cachedTechSkills = string.IsNullOrEmpty(existingJudging.TechnicalSkillsJson)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(existingJudging.TechnicalSkillsJson) ?? new List<string>();

            var cachedSoftSkills = string.IsNullOrEmpty(existingJudging.SoftSkillsJson)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(existingJudging.SoftSkillsJson) ?? new List<string>();

            var cachedGradedQuestions = string.IsNullOrEmpty(existingJudging.GradedQuestionsJson)
                ? new List<GradedQuestionDto>()
                : System.Text.Json.JsonSerializer.Deserialize<List<GradedQuestionDto>>(existingJudging.GradedQuestionsJson) ?? new List<GradedQuestionDto>();

            return new VettingEvaluationResponseDto
            {
                Score = existingJudging.Score,
                Summary = existingJudging.Summary,
                RecommendedHire = existingJudging.RecommendedHire,
                TechnicalSkills = cachedTechSkills,
                SoftSkills = cachedSoftSkills,
                HolisticAdjustment = existingJudging.HolisticAdjustment,
                HolisticAdjustmentReason = existingJudging.HolisticAdjustmentReason,
                GradedQuestions = cachedGradedQuestions
            };
        }

        // 5. Fetch questions and candidate answers if not yet judged
        var answers = await _context.Set<ProposalAnswer>()
            .AsNoTracking()
            .Include(pa => pa.JobPostQuestions)
            .Where(pa => pa.ProposalsId == request.ProposalId)
            .ToListAsync(cancellationToken);

        // 6. Construct AI request payload
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

        // 7. Call AI service
        var evalResult = await _aiServiceClient.AnalyzeVettingAsync(requestDto, cancellationToken);

        var techSkillsJson = System.Text.Json.JsonSerializer.Serialize(evalResult.TechnicalSkills ?? new List<string>());
        var softSkillsJson = System.Text.Json.JsonSerializer.Serialize(evalResult.SoftSkills ?? new List<string>());
        var gradedQuestionsJson = System.Text.Json.JsonSerializer.Serialize(evalResult.GradedQuestions ?? new List<GradedQuestionDto>());

        var newJudging = new ProposalAiJudging
        {
            ProposalAiJudgingsId = Guid.NewGuid(),
            ProposalId = request.ProposalId,
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
        await _context.SaveChangesAsync(cancellationToken);

        return evalResult;
    }
}
