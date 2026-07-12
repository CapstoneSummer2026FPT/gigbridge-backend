using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.GetDraft;
public sealed class GetPromotionDraftQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPromotionDraftQuery, PromotionDraftDto>
{
    public async Task<PromotionDraftDto> Handle(GetPromotionDraftQuery request, CancellationToken ct)
    {
        var profile = await context.Set<FreelancerProfile>().AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .Select(x => new { PhotoUrl = x.User.Avatar, DisplayName = x.User.FullName, x.Title })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Freelancer profile does not exist.");
        return new PromotionDraftDto(profile.PhotoUrl ?? string.Empty, profile.DisplayName,
            profile.Title, await PromotionPolicy.LoadAsync(context, ct));
    }
}
