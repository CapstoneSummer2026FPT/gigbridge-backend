using Application.Common.Exceptions;
using Application.Common.Interfaces.Media;
using Application.Features.Premium.Common.Interfaces;
using MediatR;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed class UploadJobPromotionImageCommandHandler(
    IMediaService media,
    IPremiumAccessService premiumAccess)
    : IRequestHandler<UploadJobPromotionImageCommand, string>
{
    private const long MaximumBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp" };

    public async Task<string> Handle(UploadJobPromotionImageCommand request, CancellationToken cancellationToken)
    {
        await premiumAccess.RequirePremiumClientAsync(request.UserId, cancellationToken);
        if (!AllowedTypes.Contains(request.ContentType))
            throw new BadRequestException("Promotion image must be JPEG, PNG, or WebP.");
        if (request.Content.CanSeek && request.Content.Length > MaximumBytes)
            throw new BadRequestException("Promotion image must not exceed 5 MB.");
        return await media.UploadFileAsync(
            request.Content, request.FileName, request.ContentType,
            "premium/job-promotions", cancellationToken);
    }
}
