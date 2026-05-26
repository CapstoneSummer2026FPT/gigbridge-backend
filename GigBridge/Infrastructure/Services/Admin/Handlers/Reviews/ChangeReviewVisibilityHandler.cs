using Application.Features.Admin.Reviews.Command;
using Application.Features.Admin.Reviews.Dto;
using Infrastructure.Services.Admin.Interfaces;
using MediatR;

namespace Infrastructure.Services.Admin.Handlers.Reviews;

public sealed class ChangeReviewVisibilityHandler : IRequestHandler<ChangeReviewVisibilityCommand, ReviewDto>
{
    private readonly IAdminReviewService _reviewService;

    public ChangeReviewVisibilityHandler(IAdminReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    public Task<ReviewDto> Handle(ChangeReviewVisibilityCommand request, CancellationToken cancellationToken) =>
        _reviewService.SetVisibilityAsync(request.ReviewId, request.Request, request.Actor, cancellationToken);
}

