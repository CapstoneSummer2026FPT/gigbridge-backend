using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Chat.Common.FinalOffers.Get.DTOs;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.FinalOffers.Get.Queries;

public sealed class GetNegotiationOfferDetailQueryHandler
    : IRequestHandler<GetNegotiationOfferDetailQuery, NegotiationOfferDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetNegotiationOfferDetailQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<NegotiationOfferDetailDto> Handle(
        GetNegotiationOfferDetailQuery request,
        CancellationToken cancellationToken)
    {
        var offer = await _context.Set<NegotiationOffer>()
            .AsNoTracking()
            .Include(item => item.NegotiationOfferMilestones)
                .ThenInclude(item => item.WorkItems)
            .FirstOrDefaultAsync(item => item.NegotiationOfferId == request.OfferId, cancellationToken);
        if (offer is null) throw new NotFoundException("Negotiation offer does not exist.");

        var canView = await _context.Set<ConversationParticipant>().AnyAsync(
            item => item.ConversationsId == offer.ConversationsId &&
                    item.UserId == request.UserId &&
                    item.LeftAt == null &&
                    item.DeletedAt == null,
            cancellationToken);
        if (!canView) throw new ForbiddenAccessException("You cannot view this final offer.");

        return new NegotiationOfferDetailDto
        {
            NegotiationOfferId = offer.NegotiationOfferId,
            ConversationId = offer.ConversationsId,
            FinalPrice = offer.FinalPrice,
            ScopeSummary = offer.ScopeSummary,
            StartDate = offer.StartDate,
            EndDate = offer.EndDate,
            ClientNote = offer.ClientNote,
            Status = offer.Status,
            CreatedAt = offer.CreatedAt,
            Milestones = offer.NegotiationOfferMilestones.OrderBy(item => item.OrderIndex).Select(item => new NegotiationMilestoneDto
            {
                Id = item.NegotiationOfferMilestoneId,
                Title = item.Title,
                Description = item.Description,
                Amount = item.Amount,
                EstimatedDuration = item.EstimatedDuration,
                DueDate = item.DueDate,
                Deliverables = item.Deliverables,
                AcceptanceCriteria = item.AcceptanceCriteria,
                OrderIndex = item.OrderIndex,
                WorkItems = item.WorkItems.OrderBy(workItem => workItem.OrderIndex).Select(workItem => new NegotiationWorkItemDto
                {
                    Id = workItem.NegotiationOfferWorkItemId,
                    Title = workItem.Title,
                    Description = workItem.Description,
                    Deliverables = workItem.Deliverables,
                    EstimatedDuration = workItem.EstimatedDuration,
                    OrderIndex = workItem.OrderIndex
                }).ToList()
            }).ToList()
        };
    }
}
