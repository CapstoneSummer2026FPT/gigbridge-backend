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

        // 4. Fetch the questions and candidate answers
        var answers = await _context.Set<ProposalAnswer>()
            .AsNoTracking()
            .Include(pa => pa.JobPostQuestions)
            .Where(pa => pa.ProposalsId == request.ProposalId)
            .ToListAsync(cancellationToken);

        // 5. Construct the AI request payload DTO
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

        // 6. Call the AI service via HttpClient
        return await _aiServiceClient.AnalyzeVettingAsync(requestDto, cancellationToken);
    }
}
