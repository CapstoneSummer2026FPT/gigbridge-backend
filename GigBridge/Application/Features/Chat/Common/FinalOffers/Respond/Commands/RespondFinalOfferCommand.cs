using Application.Features.Chat.Common.FinalOffers.Respond.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.FinalOffers.Respond.Commands;

public record RespondFinalOfferCommand(
    Guid UserId,
    RespondFinalOfferRequest Request) : IRequest<bool>;
