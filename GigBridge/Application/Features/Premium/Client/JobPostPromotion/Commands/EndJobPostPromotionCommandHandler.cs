using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using JobPostPromotionEntity = Domain.Entities.JobPostPromotion;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed class EndJobPostPromotionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService clock)
    : IRequestHandler<EndJobPostPromotionCommand, JobPostPromotionDto>
{
    public async Task<JobPostPromotionDto> Handle(
        EndJobPostPromotionCommand command,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var promotion = await context.Set<JobPostPromotionEntity>()
            .Include(item => item.JobPost)
            .Where(item =>
                item.JobPostId == command.JobPostId &&
                item.ClientUserId == command.UserId)
            .OrderByDescending(item => item.FeaturedFrom)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Job promotion does not exist.");

        if (promotion.FeaturedFrom > now || promotion.FeaturedUntil <= now)
            throw new ConflictException("Only an active job promotion can be ended early.");

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        promotion.FeaturedUntil = now;
        promotion.JobPost.IsFeatured = false;
        promotion.JobPost.FeaturedUntil = now;
        promotion.JobPost.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new JobPostPromotionDto(
            promotion.JobPostId,
            false,
            promotion.FeaturedFrom,
            promotion.FeaturedUntil,
            promotion.TokenCost,
            promotion.WalletTransactionId,
            promotion.JobPostPromotionsId,
            promotion.ImageUrl,
            promotion.PromotionTitle,
            promotion.PromotionDescription);
    }
}
