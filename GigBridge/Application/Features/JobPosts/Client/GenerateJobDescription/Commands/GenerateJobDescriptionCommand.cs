using MediatR;
using Application.Features.JobPosts.Client.GenerateJobDescription.DTOs;

namespace Application.Features.JobPosts.Client.GenerateJobDescription.Commands;

public record GenerateJobDescriptionCommand(List<string> VettingQuestions) : IRequest<GenerateJobDescriptionResponse>;
