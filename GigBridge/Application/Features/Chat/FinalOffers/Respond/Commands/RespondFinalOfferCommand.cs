using Application.Features.Chat.FinalOffers.Respond.DTOs;
using MediatR;

namespace Application.Features.Chat.FinalOffers.Respond.Commands;

public record RespondFinalOfferCommand(
    Guid UserId,
    RespondFinalOfferRequest Request) : IRequest<bool>;
