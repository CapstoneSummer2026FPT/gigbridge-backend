using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Ai;
using Application.Common.InternalServices.Premium.Interfaces;
using Application.Common.Models.Ai;
using Application.Features.Premium.Client.AiJobPostGenerator.Commands;
using Application.Features.Premium.Client.AiJobPostGenerator.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.AiJobPostGenerator.Commands;

public class GenerateJobHiringPlanCommandHandler
    : IRequestHandler<GenerateJobHiringPlanCommand, GenerateJobHiringPlanResponse>
{
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IPremiumAccessService _premiumAccess;

    public GenerateJobHiringPlanCommandHandler(
        IAiServiceClient aiServiceClient,
        IPremiumAccessService premiumAccess)
    {
        _aiServiceClient = aiServiceClient;
        _premiumAccess = premiumAccess;
    }

    public async Task<GenerateJobHiringPlanResponse> Handle(
        GenerateJobHiringPlanCommand command,
        CancellationToken cancellationToken)
    {
        await _premiumAccess.RequirePremiumClientAsync(command.UserId, cancellationToken);

        var aiRequest = new JobPostHiringPlanGenerationRequestDto
        {
            ClientPrompt = command.ClientPrompt,
            Title = command.Title,
            Description = command.Description,
            BudgetMin = command.BudgetMin,
            BudgetMax = command.BudgetMax,
            EstimatedDuration = command.EstimatedDuration,
            ProposalClosingDate = command.ProposalClosingDate
        };

        JobPostHiringPlanGenerationResponseDto aiResponse;
        try
        {
            aiResponse = await _aiServiceClient.GenerateJobHiringPlanAsync(aiRequest, cancellationToken);
        }
        catch (Application.Common.Exceptions.ExternalServiceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new Application.Common.Exceptions.ExternalServiceException(
                "AI service is temporarily unavailable. Please try again later.", exception);
        }

        return new GenerateJobHiringPlanResponse
        {
            QuestionRecruitment = aiResponse.QuestionRecruitment,
            Milestones = aiResponse.Milestones?.Select(m => new GeneratedJobPostMilestoneDto
            {
                Title = m.Title,
                Amount = m.Amount,
                EstimatedDuration = m.EstimatedDuration,
                DueDate = m.DueDate,
                Description = m.Description,
                Deliverables = m.Deliverables,
                AcceptanceCriteria = m.AcceptanceCriteria
            }).ToList() ?? new()
        };
    }
}
