using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Scheduling;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;
using Application.Features.JobPosts.Common;
using Application.Features.Proposals.Common;
using Domain.Entities;
using Domain.Enums.Chat;
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

        var conversation = await _context.Set<Conversation>().AsNoTracking().FirstOrDefaultAsync(
            item => item.ConversationsId == command.ConversationId && item.DeletedAt == null,
            cancellationToken);
        if (conversation is null) throw new NotFoundException("Conversation does not exist.");

        if (conversation.ConversationType != (int)ConversationType.JobNegotiation)
        {
            throw new BadRequestException("Negotiation milestone plans can only be edited in job negotiation conversations.");
        }

        if (!conversation.JobPostsId.HasValue)
        {
            throw new BadRequestException("Job negotiation conversation must be attached to a job post.");
        }

        await JobPostNegotiationGuard.EnsureEligibleForNegotiationAsync(
            _context,
            conversation.JobPostsId.Value,
            cancellationToken);

        if (command.Request.Milestones.Any(item => !ProposalTotalsCalculator.IsValidDraftAmount(item.Amount)))
        {
            throw new BadRequestException("Milestone amounts cannot be negative, may use at most 2 decimal places, and must fit decimal(18,2).");
        }

        var existing = await _context.Set<NegotiationMilestoneDraft>()
            .Where(item => item.ConversationsId == command.ConversationId)
            .ToListAsync(cancellationToken);
        _context.Set<NegotiationMilestoneDraft>().RemoveRange(existing);

        var now = _dateTimeService.UtcNow;
        var orderedMilestones = command.Request.Milestones.OrderBy(item => item.OrderIndex).ToList();
        var computedDueDates = MilestoneDeadlineCalculator.CalculateDueDates(
            DateOnly.FromDateTime(_dateTimeService.UtcNow),
            orderedMilestones.Select(item => item.EstimatedDuration).ToList());
        var drafts = orderedMilestones.Select((item, index) =>
        {
            var draft = new NegotiationMilestoneDraft
            {
                NegotiationMilestoneDraftId = Guid.NewGuid(),
                ConversationsId = command.ConversationId,
                Title = item.Title?.Trim() ?? string.Empty,
                Description = Clean(item.Description),
                Amount = item.Amount,
                EstimatedDuration = Clean(item.EstimatedDuration),
                DueDate = computedDueDates[index],
                Deliverables = item.Deliverables?.Trim() ?? string.Empty,
                AcceptanceCriteria = item.AcceptanceCriteria?.Trim() ?? string.Empty,
                OrderIndex = index,
                CreatedAt = now,
                UpdatedAt = now
            };
            draft.WorkItems = item.WorkItems.OrderBy(workItem => workItem.OrderIndex).Select((workItem, workIndex) => new NegotiationMilestoneDraftWorkItem
            {
                NegotiationMilestoneDraftWorkItemId = Guid.NewGuid(),
                NegotiationMilestoneDraftId = draft.NegotiationMilestoneDraftId,
                Title = workItem.Title?.Trim() ?? string.Empty,
                Description = Clean(workItem.Description),
                Deliverables = Clean(workItem.Deliverables),
                EstimatedDuration = Clean(workItem.EstimatedDuration),
                OrderIndex = workIndex
            }).ToList();
            return draft;
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
            OrderIndex = item.OrderIndex,
            WorkItems = item.WorkItems.OrderBy(workItem => workItem.OrderIndex).Select(workItem => new NegotiationWorkItemDto
            {
                Id = workItem.NegotiationMilestoneDraftWorkItemId,
                Title = workItem.Title,
                Description = workItem.Description,
                Deliverables = workItem.Deliverables,
                EstimatedDuration = workItem.EstimatedDuration,
                OrderIndex = workItem.OrderIndex
            }).ToList()
        }).ToList();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
