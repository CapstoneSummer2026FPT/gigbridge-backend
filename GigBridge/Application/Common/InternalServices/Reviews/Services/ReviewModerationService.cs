using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Elo.Interfaces;
using Application.Common.InternalServices.Reviews.Interfaces;
using Application.Common.InternalServices.Reviews.Models;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Reviews;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.Reviews.Services;
public sealed class ReviewModerationService : IReviewModerationService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IUserEloService _userEloService;

    public ReviewModerationService(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IUserEloService userEloService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _userEloService = userEloService;
    }

    public async Task<ReviewModerationResult> SetStatusAsync(
        Guid reviewId,
        ReviewModerationStatus status,
        Guid adminId,
        string note,
        CancellationToken cancellationToken)
    {
        var adminExists = await _context.Set<User>()
            .AnyAsync(user => user.UserId == adminId && user.Role == (int)UserRole.Admin, cancellationToken);
        if (!adminExists)
        {
            throw new ForbiddenAccessException("Only administrators can moderate reviews.");
        }

        var review = await _context.Set<Review>()
            .FirstOrDefaultAsync(item => item.ReviewsId == reviewId, cancellationToken)
            ?? throw new NotFoundException("Review does not exist.");

        if (review.ModerationStatus == (int)status)
        {
            return new ReviewModerationResult(review, false, 0);
        }

        var operationId = Guid.NewGuid();
        var now = _dateTimeService.UtcNow;
        var oldStatus = (ReviewModerationStatus)review.ModerationStatus;
        var eloDelta = await _userEloService.ApplyReviewModerationAsync(
            review.ReviewsId,
            review.RevieweeId,
            operationId,
            status == ReviewModerationStatus.Hidden,
            cancellationToken);

        review.ModerationStatus = (int)status;
        review.ModeratedByAdminId = adminId;
        review.ModeratedAt = now;
        review.ModerationNote = note.Trim();
        review.UpdatedAt = now;

        _context.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(),
            AdminId = adminId,
            Action = status == ReviewModerationStatus.Hidden ? "Review.Hidden" : "Review.Restored",
            EntityId = review.ReviewsId,
            EntityType = nameof(Review),
            OldValues = JsonSerializer.Serialize(new { moderationStatus = oldStatus.ToString() }),
            NewValues = JsonSerializer.Serialize(new
            {
                moderationStatus = status.ToString(),
                note = review.ModerationNote,
                eloDelta,
                operationId
            }),
            CreatedAt = now
        });

        return new ReviewModerationResult(review, true, eloDelta);
    }
}
