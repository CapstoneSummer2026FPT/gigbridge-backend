using MediatR;
using Application.Features.Premium.Client.AiJobPostGenerator.DTOs;

namespace Application.Features.Premium.Client.AiJobPostGenerator.Commands;

public record GenerateJobHiringPlanCommand(Guid UserId, string ClientPrompt, string Title, string Description) : IRequest<GenerateJobHiringPlanResponse>;
