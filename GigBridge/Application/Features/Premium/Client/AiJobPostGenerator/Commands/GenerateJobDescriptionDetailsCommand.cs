using MediatR;
using Application.Features.Premium.Client.AiJobPostGenerator.DTOs;

namespace Application.Features.Premium.Client.AiJobPostGenerator.Commands;

public record GenerateJobDescriptionDetailsCommand(Guid UserId, string ClientPrompt) : IRequest<GenerateJobDescriptionDetailsResponse>;
