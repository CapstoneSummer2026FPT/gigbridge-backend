using Application.Common.Interfaces;
using Application.Features.Reviews.Common;
using Application.Features.Reviews.Common.DTOs;
using Application.Common.InternalServices.Reviews.Interfaces;
using Application.Common.InternalServices.Reviews.Models;
using Application.Common.InternalServices.Reviews.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reviews.Admin.ModerateReview.Commands;

public sealed class ModerateReviewCommandHandler : IRequestHandler<ModerateReviewCommand, ManagedReviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IReviewModerationService _moderationService;

    public ModerateReviewCommandHandler(IApplicationDbContext context, IReviewModerationService moderationService)
    {
        _context = context;
        _moderationService = moderationService;
    }

    public async Task<ManagedReviewDto> Handle(ModerateReviewCommand command, CancellationToken cancellationToken)
    {
        var result = await _moderationService.SetStatusAsync(
            command.ReviewId,
            command.Request.Status,
            command.AdminId,
            command.Request.Note,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var review = await _context.Set<Domain.Entities.Review>()
            .AsNoTracking()
            .Include(item => item.Contracts)
            .Include(item => item.Reviewer)
            .Include(item => item.Reviewee)
            .FirstAsync(item => item.ReviewsId == command.ReviewId, cancellationToken);
        return ReviewManagementProjection.ToDto(review, revealAnonymousReviewer: true);
    }
}
