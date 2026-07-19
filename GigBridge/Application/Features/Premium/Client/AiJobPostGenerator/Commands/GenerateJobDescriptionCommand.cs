using MediatR;
using Application.Features.Premium.Client.AiJobPostGenerator.DTOs;

namespace Application.Features.Premium.Client.AiJobPostGenerator.Commands;

public record GenerateJobDescriptionCommand(Guid UserId, string ClientPrompt) : IRequest<GenerateJobDescriptionResponse>;
