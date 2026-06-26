using MediatR;
using Application.Features.JobPosts.Client.GenerateJobDescription.DTOs;

namespace Application.Features.JobPosts.Client.GenerateJobDescription.Commands;

public record GenerateJobDescriptionCommand(string ClientPrompt) : IRequest<GenerateJobDescriptionResponse>;
