using Application.Features.Chat.Common.FinalOffers.Get.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.FinalOffers.Get.Queries;

public sealed record GetNegotiationOfferDetailQuery(Guid OfferId, Guid UserId)
    : IRequest<NegotiationOfferDetailDto>;
