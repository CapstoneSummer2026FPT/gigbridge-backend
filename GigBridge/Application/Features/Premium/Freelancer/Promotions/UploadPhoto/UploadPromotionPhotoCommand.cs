using MediatR;
namespace Application.Features.Premium.Freelancer.Promotions.UploadPhoto;
public sealed record UploadPromotionPhotoCommand(Guid UserId, Stream Content, string FileName, string ContentType) : IRequest<string>;
