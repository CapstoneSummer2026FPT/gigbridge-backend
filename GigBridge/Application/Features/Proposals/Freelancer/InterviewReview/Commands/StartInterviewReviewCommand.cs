using Application.Common.InternalServices.Proposals.Models;
using MediatR;

namespace Application.Features.Proposals.Freelancer.InterviewReview.Commands;

public record StartInterviewReviewCommand(
    Guid ProposalId,
    Guid UserId) : IRequest<InterviewReviewSessionDto>;
