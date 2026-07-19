using Application.Features.Premium.Client.AiInterviews.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.AiInterviews.GetResults.Queries;

public sealed record GetAiInterviewResultsQuery(Guid UserId, Guid JobPostId, Guid InterviewId)
    : IRequest<AiInterviewResultsDto>;
