using Application.Common.Exceptions;
using Application.Common.Interfaces.Media;
using Application.Features.Premium.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Features.Premium.Freelancer.Promotions.Common;
using MediatR;
namespace Application.Features.Premium.Freelancer.Promotions.UploadPhoto;
public sealed class UploadPromotionPhotoCommandHandler(IMediaService media, IPremiumAccessService premium, IApplicationDbContext context)
    : IRequestHandler<UploadPromotionPhotoCommand, string>
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
    public async Task<string> Handle(UploadPromotionPhotoCommand request, CancellationToken ct)
    {
        await premium.RequirePremiumFreelancerAsync(request.UserId, ct);
        var policy = await PromotionPolicy.LoadAsync(context, ct);
        if (!AllowedTypes.Contains(request.ContentType)) throw new BadRequestException("Promotion photo must be JPEG, PNG, or WebP.");
        if (request.Content.CanSeek && request.Content.Length > policy.MaximumPhotoBytes)
            throw new BadRequestException("Promotion photo exceeds the configured size limit.");
        return await media.UploadFileAsync(request.Content, request.FileName, request.ContentType, "premium/promotions", ct);
    }
}
