using Application.Features.Reviews.Common.DTOs;
using Domain.Enums.Reviews;
using MediatR;

namespace Application.Features.Reviews.Admin.ModerateReview.Commands;

public sealed record ModerateReviewRequest(ReviewModerationStatus Status, string Note);

public sealed record ModerateReviewCommand(
    Guid ReviewId,
    Guid AdminId,
    ModerateReviewRequest Request) : IRequest<ManagedReviewDto>;
