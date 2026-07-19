using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using JobPostPromotionEntity = Domain.Entities.JobPostPromotion;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed class TrackJobPromotionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService clock) : IRequestHandler<TrackJobPromotionCommand, JobPromotionInteractionDto>
{
    public async Task<JobPromotionInteractionDto> Handle(
        TrackJobPromotionCommand request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var query = context.Set<JobPostPromotionEntity>().Where(x =>
            x.JobPostPromotionsId == request.PromotionId &&
            x.FeaturedFrom <= now && x.FeaturedUntil > now);
        var affected = request.Type == JobPromotionInteractionType.Impression
            ? await query.ExecuteUpdateAsync(update => update.SetProperty(
                x => x.ImpressionCount, x => x.ImpressionCount + 1), cancellationToken)
            : await query.ExecuteUpdateAsync(update => update.SetProperty(
                x => x.ClickCount, x => x.ClickCount + 1), cancellationToken);
        if (affected == 0) throw new NotFoundException("Active job promotion does not exist.");
        return await context.Set<JobPostPromotionEntity>().AsNoTracking()
            .Where(x => x.JobPostPromotionsId == request.PromotionId)
            .Select(x => new JobPromotionInteractionDto(
                x.JobPostPromotionsId, x.ImpressionCount, x.ClickCount))
            .SingleAsync(cancellationToken);
    }
}
