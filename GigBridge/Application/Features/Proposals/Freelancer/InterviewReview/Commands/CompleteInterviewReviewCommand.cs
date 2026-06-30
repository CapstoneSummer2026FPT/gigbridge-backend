using Application.Features.Proposals.Freelancer.InterviewReview.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.InterviewReview.Commands;

public record CompleteInterviewReviewCommand(
    Guid ProposalId,
    Guid UserId) : IRequest<InterviewReviewSessionDto>;
