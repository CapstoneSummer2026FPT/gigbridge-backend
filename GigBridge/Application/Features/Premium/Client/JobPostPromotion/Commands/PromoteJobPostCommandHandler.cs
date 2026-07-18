using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Client.JobPostPromotion.Common;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using JobPostPromotionEntity = Domain.Entities.JobPostPromotion;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed class PromoteJobPostCommandHandler(
    IApplicationDbContext context,
    IPremiumAccessService premiumAccess,
    IWalletLedgerService walletLedger,
    IDateTimeService clock) : IRequestHandler<PromoteJobPostCommand, JobPostPromotionDto>
{
    public async Task<JobPostPromotionDto> Handle(
        PromoteJobPostCommand command,
        CancellationToken cancellationToken)
    {
        await premiumAccess.RequirePremiumClientAsync(command.UserId, cancellationToken);
        var idempotencyKey = command.Request.IdempotencyKey.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            throw new BadRequestException("A valid idempotency key is required.");
        if (!Uri.TryCreate(command.Request.ImageUrl?.Trim(), UriKind.Absolute, out var imageUri) ||
            (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
            throw new BadRequestException("A valid HTTP(S) promotion image URL is required.");
        if (string.IsNullOrWhiteSpace(command.Request.PromotionTitle) || command.Request.PromotionTitle.Trim().Length > 140)
            throw new BadRequestException("Promotion title is required and must not exceed 140 characters.");
        if (string.IsNullOrWhiteSpace(command.Request.PromotionDescription) || command.Request.PromotionDescription.Trim().Length > 1000)
            throw new BadRequestException("Promotion description is required and must not exceed 1000 characters.");
        var existing = await context.Set<JobPostPromotionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClientUserId == command.UserId &&
                x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.JobPostId != command.JobPostId)
                throw new ConflictException("The idempotency key was already used for a different promotion.");
            return Map(existing);
        }

        var jobPost = await context.Set<JobPost>()
            .Include(x => x.ClientProfiles)
            .FirstOrDefaultAsync(x => x.JobPostsId == command.JobPostId &&
                x.ClientProfiles.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("Job post not found.");
        if (jobPost.Status != 1)
            throw new ConflictException("Only an open job post can be promoted.");

        var now = clock.UtcNow;
        if (jobPost.IsFeatured && jobPost.FeaturedUntil > now)
            throw new ConflictException("The job post already has an active promotion.");
        var policy = await JobPromotionPolicy.LoadAsync(context, cancellationToken);
        JobPromotionPolicy.Validate(policy.TokenCost, policy.DurationDays);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        WalletTransaction walletTransaction;
        try
        {
            walletTransaction = await walletLedger.DebitAsync(
                command.UserId,
                policy.TokenCost,
                WalletTransactionType.PromotionPurchase,
                $"job-promotion:{idempotencyKey}",
                JsonSerializer.Serialize(new { jobPostId = command.JobPostId }),
                cancellationToken);
        }
        catch (BadRequestException exception) when (exception.Message == "Insufficient wallet balance.")
        {
            throw new BadRequestException("Insufficient balance. Please top up your wallet.");
        }

        var featuredUntil = now.AddDays(policy.DurationDays);
        jobPost.IsFeatured = true;
        jobPost.FeaturedFrom = now;
        jobPost.FeaturedUntil = featuredUntil;
        jobPost.UpdatedAt = now;
        var promotion = new JobPostPromotionEntity
        {
            JobPostPromotionsId = Guid.NewGuid(),
            JobPostId = jobPost.JobPostsId,
            ClientUserId = command.UserId,
            WalletTransactionId = walletTransaction.WalletTransactionsId,
            IdempotencyKey = idempotencyKey,
            TokenCost = policy.TokenCost,
            ImageUrl = command.Request.ImageUrl.Trim(),
            PromotionTitle = command.Request.PromotionTitle.Trim(),
            PromotionDescription = command.Request.PromotionDescription.Trim(),
            FeaturedFrom = now,
            FeaturedUntil = featuredUntil,
            CreatedAt = now
        };
        context.Set<JobPostPromotionEntity>().Add(promotion);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(promotion);
    }

    private static JobPostPromotionDto Map(JobPostPromotionEntity promotion) => new(
        promotion.JobPostId,
        true,
        promotion.FeaturedFrom,
        promotion.FeaturedUntil,
        promotion.TokenCost,
        promotion.WalletTransactionId,
        promotion.JobPostPromotionsId,
        promotion.ImageUrl,
        promotion.PromotionTitle,
        promotion.PromotionDescription);
}
