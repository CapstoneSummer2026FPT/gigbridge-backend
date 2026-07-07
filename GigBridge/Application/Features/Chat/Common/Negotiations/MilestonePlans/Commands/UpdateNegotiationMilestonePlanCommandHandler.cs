using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Negotiations.MilestonePlans.Commands;

public sealed class UpdateNegotiationMilestonePlanCommandHandler
    : IRequestHandler<UpdateNegotiationMilestonePlanCommand, IReadOnlyCollection<NegotiationMilestoneDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateNegotiationMilestonePlanCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<IReadOnlyCollection<NegotiationMilestoneDto>> Handle(
        UpdateNegotiationMilestonePlanCommand command,
        CancellationToken cancellationToken)
    {
        var isClient = await _context.Set<ConversationParticipant>().AnyAsync(
            item => item.ConversationsId == command.ConversationId &&
                    item.UserId == command.UserId &&
                    item.ParticipantRole == (int)ParticipantRole.Client &&
                    item.LeftAt == null &&
                    item.DeletedAt == null,
            cancellationToken);

        if (!isClient) throw new ForbiddenAccessException("Only the client can edit the negotiation milestone plan.");

        var conversationExists = await _context.Set<Conversation>().AnyAsync(
            item => item.ConversationsId == command.ConversationId && item.DeletedAt == null,
            cancellationToken);
        if (!conversationExists) throw new NotFoundException("Conversation does not exist.");

        var existing = await _context.Set<NegotiationMilestoneDraft>()
            .Where(item => item.ConversationsId == command.ConversationId)
            .ToListAsync(cancellationToken);
        _context.Set<NegotiationMilestoneDraft>().RemoveRange(existing);

        var now = _dateTimeService.UtcNow;
        var drafts = command.Request.Milestones.Select((item, index) => new NegotiationMilestoneDraft
        {
            NegotiationMilestoneDraftId = Guid.NewGuid(),
            ConversationsId = command.ConversationId,
            Title = item.Title?.Trim() ?? string.Empty,
            Description = Clean(item.Description),
            Amount = item.Amount,
            EstimatedDuration = Clean(item.EstimatedDuration),
            DueDate = item.DueDate,
            Deliverables = item.Deliverables?.Trim() ?? string.Empty,
            AcceptanceCriteria = item.AcceptanceCriteria?.Trim() ?? string.Empty,
            OrderIndex = index,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        _context.Set<NegotiationMilestoneDraft>().AddRange(drafts);
        await _context.SaveChangesAsync(cancellationToken);

        return drafts.Select(item => new NegotiationMilestoneDto
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
        }).ToList();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
