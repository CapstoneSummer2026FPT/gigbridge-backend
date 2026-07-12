using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Negotiations.MilestonePlans.Queries;

public sealed class GetNegotiationMilestonePlanQueryHandler
    : IRequestHandler<GetNegotiationMilestonePlanQuery, IReadOnlyCollection<NegotiationMilestoneDto>>
{
    private readonly IApplicationDbContext _context;

    public GetNegotiationMilestonePlanQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyCollection<NegotiationMilestoneDto>> Handle(
        GetNegotiationMilestonePlanQuery request,
        CancellationToken cancellationToken)
    {
        var canView = await _context.Set<ConversationParticipant>().AnyAsync(
            item => item.ConversationsId == request.ConversationId &&
                    item.UserId == request.UserId &&
                    item.LeftAt == null &&
                    item.DeletedAt == null,
            cancellationToken);

        if (!canView) throw new ForbiddenAccessException("You are not a participant in this negotiation.");

        return await _context.Set<NegotiationMilestoneDraft>()
            .AsNoTracking()
            .Where(item => item.ConversationsId == request.ConversationId)
            .OrderBy(item => item.OrderIndex)
            .Select(item => new NegotiationMilestoneDto
            {
                Id = item.NegotiationMilestoneDraftId,
                Title = item.Title,
                Description = item.Description,
                Amount = item.Amount,
                EstimatedDuration = item.EstimatedDuration,
                DueDate = item.DueDate,
                Deliverables = item.Deliverables,
                AcceptanceCriteria = item.AcceptanceCriteria,
                OrderIndex = item.OrderIndex
            })
            .ToListAsync(cancellationToken);
    }
}
