using MediatR;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed record UploadJobPromotionImageCommand(
    Guid UserId,
    Stream Content,
    string FileName,
    string ContentType) : IRequest<string>;
