using MediatR;
using Application.Features.Premium.Client.AiJobPostGenerator.DTOs;

namespace Application.Features.Premium.Client.AiJobPostGenerator.Commands;

public record GenerateJobHiringPlanCommand(Guid UserId, string ClientPrompt, string Title, string Description, decimal? BudgetMin, decimal? BudgetMax, string? EstimatedDuration, string ProposalClosingDate) : IRequest<GenerateJobHiringPlanResponse>;
