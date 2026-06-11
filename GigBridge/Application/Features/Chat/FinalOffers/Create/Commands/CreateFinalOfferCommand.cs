using Application.Features.Chat.FinalOffers.Create.DTOs;
using MediatR;

namespace Application.Features.Chat.FinalOffers.Create.Commands;

public record CreateFinalOfferCommand(
    Guid UserId,
    CreateFinalOfferRequest Request) : IRequest<Guid>;
