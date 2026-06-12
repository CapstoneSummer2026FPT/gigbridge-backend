using Application.Features.Chat.Common.FinalOffers.Create.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.FinalOffers.Create.Commands;

public record CreateFinalOfferCommand(
    Guid UserId,
    CreateFinalOfferRequest Request) : IRequest<Guid>;
