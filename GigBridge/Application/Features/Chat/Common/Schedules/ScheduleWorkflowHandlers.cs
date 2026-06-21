using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Schedules;

public sealed class ScheduleWorkflowHandlers :
    IRequestHandler<CreateScheduleCommand, ScheduleMutationResult>,
    IRequestHandler<UpdateScheduleCommand, ScheduleMutationResult>,
    IRequestHandler<CancelScheduleCommand, ScheduleMutationResult>,
    IRequestHandler<GetScheduleQuery, ScheduleResponse>,
    IRequestHandler<GetOngoingScheduleQuery, OngoingScheduleResponse>
{
    private const int MaxEdits = 2;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IChatRealtimeNotifier _chat;

    public ScheduleWorkflowHandlers(IApplicationDbContext context, IDateTimeService clock, IChatRealtimeNotifier chat)
    {
        _context = context;
        _clock = clock;
        _chat = chat;
    }

    public async Task<ScheduleMutationResult> Handle(CreateScheduleCommand command, CancellationToken ct)
    {
        var now = Utc(_clock.UtcNow);
        var title = NormalizeTitle(command.Request.Title);
        var details = NormalizeDetails(command.Request.Details);
        var startsAt = NormalizeInstant(command.Request.ScheduledAt);
        ValidateFields(title, details, startsAt, now);

        var conversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(x => x.ConversationsId == command.Request.ConversationId, ct)
            ?? throw new NotFoundException("Conversation does not exist.");

        if (conversation.Status != (int)ConversationStatus.Active ||
            conversation.ConversationType is not ((int)ConversationType.JobNegotiation) and
                not ((int)ConversationType.ContractWorkroom) and not ((int)ConversationType.JobInvitedRoom))
            throw new BadRequestException("Schedules can only be created in an active client-freelancer chat.");

        var participants = await ActiveParticipants(conversation.ConversationsId, ct);
        EnsureClientFreelancerConversation(participants, command.UserId);
        var scheduled = await _context.Set<Schedule>()
            .Where(x => x.ConversationId == conversation.ConversationsId &&
                x.Status == ScheduleStatus.Scheduled)
            .ToListAsync(ct);
        if (scheduled.Any(x => x.ScheduledAtUtc > now))
            throw new ConflictException("This conversation already has an ongoing schedule.");

        foreach (var elapsed in scheduled)
        {
            elapsed.Status = ScheduleStatus.Completed;
            elapsed.UpdatedAt = now;
            elapsed.Version++;
        }
        var actor = participants.Single(x => x.UserId == command.UserId).User;

        var schedule = new Schedule
        {
            ScheduleId = Guid.NewGuid(), ConversationId = conversation.ConversationsId,
            CreatedByUserId = command.UserId, Title = title, Details = details,
            ScheduledAtUtc = startsAt, TimeZoneId = command.Request.TimeZoneId, Status = ScheduleStatus.Scheduled,
            EditCount = 0, Version = 1, CreatedAt = now
        };
        _context.Set<Schedule>().Add(schedule);
        return await PersistEvent(schedule, conversation, participants, actor, ScheduleEventType.Created, null, now, ct);
    }

    public async Task<ScheduleMutationResult> Handle(UpdateScheduleCommand command, CancellationToken ct)
    {
        var now = Utc(_clock.UtcNow);
        var schedule = await LoadSchedule(command.ScheduleId, ct);
        var participants = await ActiveParticipants(schedule.ConversationId, ct);
        EnsureParticipant(participants, command.UserId);
        if (schedule.Conversation.Status != (int)ConversationStatus.Active)
            throw new BadRequestException("Schedules in a closed or archived conversation cannot be edited.");
        EnsureMutable(schedule, command.UserId, now, isEdit: true);
        if (schedule.Version != command.Request.ExpectedVersion)
            throw new ConflictException("The schedule changed. Refresh it before using another edit.");
        if (schedule.EditCount >= MaxEdits)
            throw new BadRequestException("This schedule has used both available edits.");

        var title = NormalizeTitle(command.Request.Title);
        var details = NormalizeDetails(command.Request.Details);
        var startsAt = NormalizeInstant(command.Request.ScheduledAt);
        ValidateFields(title, details, startsAt, now);
        if (schedule.Title == title && schedule.Details == details && schedule.ScheduledAtUtc == startsAt)
            throw new BadRequestException("The update does not change the schedule.");

        schedule.Title = title; schedule.Details = details; schedule.ScheduledAtUtc = startsAt;
        schedule.EditCount++; schedule.Version++; schedule.UpdatedAt = now;
        var actor = participants.Single(x => x.UserId == command.UserId).User;
        return await PersistEvent(schedule, schedule.Conversation, participants, actor, ScheduleEventType.Edited, null, now, ct);
    }

    public async Task<ScheduleMutationResult> Handle(CancelScheduleCommand command, CancellationToken ct)
    {
        var now = Utc(_clock.UtcNow);
        var schedule = await LoadSchedule(command.ScheduleId, ct);
        var participants = await ActiveParticipants(schedule.ConversationId, ct);
        EnsureParticipant(participants, command.UserId);
        EnsureMutable(schedule, command.UserId, now, isEdit: false);
        if (schedule.Version != command.Request.ExpectedVersion)
            throw new ConflictException("The schedule changed. Refresh it before cancelling.");
        var reason = NormalizeDetails(command.Request.Reason);
        if (string.IsNullOrWhiteSpace(reason)) throw new BadRequestException("A cancellation reason is required.");
        if (reason.Length > 1000) throw new BadRequestException("Cancellation reason cannot exceed 1000 characters.");

        schedule.Status = ScheduleStatus.Cancelled; schedule.CancelledByUserId = command.UserId;
        schedule.CancellationReason = reason; schedule.CancelledAt = now; schedule.UpdatedAt = now; schedule.Version++;
        var actor = participants.Single(x => x.UserId == command.UserId).User;
        return await PersistEvent(schedule, schedule.Conversation, participants, actor, ScheduleEventType.Cancelled, reason, now, ct);
    }

    public async Task<ScheduleResponse> Handle(GetScheduleQuery query, CancellationToken ct)
    {
        var schedule = await LoadSchedule(query.ScheduleId, ct);
        var participants = await ActiveParticipants(schedule.ConversationId, ct);
        EnsureParticipant(participants, query.UserId);
        return ToResponse(schedule, query.UserId, Utc(_clock.UtcNow));
    }

    public async Task<OngoingScheduleResponse> Handle(GetOngoingScheduleQuery query, CancellationToken ct)
    {
        var participants = await ActiveParticipants(query.ConversationId, ct);
        EnsureParticipant(participants, query.UserId);
        var now = Utc(_clock.UtcNow);
        var ongoing = await _context.Set<Schedule>().AsNoTracking()
            .Where(x => x.ConversationId == query.ConversationId &&
                x.Status == ScheduleStatus.Scheduled && x.ScheduledAtUtc > now)
            .OrderBy(x => x.ScheduledAtUtc)
            .Select(x => new { x.ScheduleId, x.ScheduledAtUtc })
            .FirstOrDefaultAsync(ct);
        return ongoing is null
            ? new OngoingScheduleResponse(false, null, null)
            : new OngoingScheduleResponse(true, ongoing.ScheduleId, ongoing.ScheduledAtUtc);
    }

    private async Task<ScheduleMutationResult> PersistEvent(Schedule schedule, Conversation conversation,
        List<ConversationParticipant> participants, User actor, ScheduleEventType eventType, string? reason,
        DateTime now, CancellationToken ct)
    {
        var message = new Message
        {
            MessagesId = Guid.NewGuid(), ConversationsId = conversation.ConversationsId,
            SenderUserId = actor.UserId, MessageType = (int)MessageType.Schedule,
            Content = eventType switch { ScheduleEventType.Created => $"Scheduled: {schedule.Title}", ScheduleEventType.Edited => $"Schedule updated: {schedule.Title}", _ => $"Schedule cancelled: {schedule.Title}" },
            ScheduleId = schedule.ScheduleId, ScheduleEventType = eventType,
            ScheduleEventSequence = schedule.Version, SentAt = now
        };
        var eventDto = ToEvent(schedule, message.MessagesId, eventType, actor, reason);
        message.Metadata = JsonSerializer.Serialize(eventDto, JsonOptions);
        _context.Set<Message>().Add(message);

        conversation.LastMessageId = message.MessagesId; conversation.LastMessageAt = now; conversation.UpdatedAt = now;
        foreach (var p in participants.Where(x => x.UserId != actor.UserId).GroupBy(x => x.UserId).Select(x => x.First()))
            p.UnreadCount++;

        foreach (var participant in participants.GroupBy(x => x.UserId).Select(x => x.First()))
        {
            var snapshot = PersonalizeEvent(eventDto, schedule, participant.UserId, now);
            var metadata = JsonSerializer.Serialize(snapshot, JsonOptions);
            var existing = await _context.Set<Notification>().FirstOrDefaultAsync(n =>
                n.UserId == participant.UserId && n.Type == (int)NotificationType.Schedule &&
                n.ReferenceId == schedule.ScheduleId && n.IsRead != true, ct);
            var self = participant.UserId == actor.UserId;
            var title = NotificationTitle(eventType, self);
            var content = $"{actor.FullName}: {schedule.Title} — {FormatVietnamTime(schedule.ScheduledAtUtc)}";
            var notification = existing ?? new Notification { NotificationsId = Guid.NewGuid(), UserId = participant.UserId };
            if (existing is null) _context.Set<Notification>().Add(notification);
            if ((notification.Revision ?? 0) < schedule.Version)
            {
                notification.Type = (int)NotificationType.Schedule; notification.Title = title;
                notification.Content = content; notification.ReferenceId = schedule.ScheduleId;
                notification.ReferenceType = "Schedule"; notification.Metadata = metadata;
                notification.Revision = schedule.Version; notification.IsRead = false; notification.ReadAt = null; notification.CreatedAt = now;
            }

            AddOutbox(schedule, participant.UserId, notification.NotificationsId, eventType, actor, participant.User, metadata, now);
        }

        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new ConflictException("The schedule was changed by the other participant.", ex); }
        catch (DbUpdateException ex) { throw new ConflictException("A concurrent schedule event was already persisted. Refresh and retry.", ex); }

        MessageResponse? actorMessageResponse = null;
        foreach (var participant in participants.GroupBy(x => x.UserId).Select(x => x.First()))
        {
            var snapshot = PersonalizeEvent(eventDto, schedule, participant.UserId, now);
            var metadata = JsonSerializer.Serialize(snapshot, JsonOptions);
            var messageResponse = new MessageResponse(message.MessagesId, message.ConversationsId, message.SenderUserId,
                message.MessageType, message.Content, null, metadata, null, message.SentAt, null, false, [], snapshot);

            await _chat.SendUserEventAsync(participant.UserId, "ReceiveMessage", messageResponse, ct);
            await _chat.SendUserEventAsync(participant.UserId, "ScheduleChanged", snapshot, ct);
            await _chat.SendUserEventAsync(participant.UserId, "ConversationUpdated", new
            {
                conversationId = schedule.ConversationId,
                lastMessage = messageResponse,
                lastMessageAt = now,
                unreadCount = participant.UnreadCount
            }, ct);

            if (participant.UserId == actor.UserId)
                actorMessageResponse = messageResponse;
        }

        return new ScheduleMutationResult(
            ToResponse(schedule, actor.UserId, now),
            actorMessageResponse ?? throw new InvalidOperationException("The schedule actor is not an active participant."));
    }

    private void AddOutbox(Schedule schedule, Guid recipientId, Guid notificationId, ScheduleEventType type,
        User actor, User recipient, string metadata, DateTime now)
    {
        var payload = JsonSerializer.Serialize(new ScheduleDeliveryPayload(notificationId, recipientId, recipient.Email,
            type == ScheduleEventType.Cancelled ? "GigBridge schedule cancelled" : type == ScheduleEventType.Edited ? "GigBridge schedule updated" : "GigBridge schedule confirmation",
            BuildEmail(schedule, type, actor), metadata), JsonOptions);
        foreach (var channel in new[] { DeliveryChannel.NotificationRealtime, DeliveryChannel.Email })
            _context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
            {
                DeliveryOutboxId = Guid.NewGuid(), DeliveryKey = $"schedule:{schedule.ScheduleId}:{schedule.Version}:{recipientId}:{(int)channel}",
                ScheduleId = schedule.ScheduleId, RecipientUserId = recipientId, EventSequence = schedule.Version,
                Channel = (int)channel, Payload = payload, Status = (int)DeliveryOutboxStatus.Pending,
                NextAttemptAt = now, CreatedAt = now
            });
    }

    private static string BuildEmail(Schedule s, ScheduleEventType type, User actor) =>
        $"<h2>Schedule {WebUtility.HtmlEncode(type.ToString().ToLowerInvariant())}</h2>" +
        $"<p><strong>{WebUtility.HtmlEncode(s.Title)}</strong></p>" +
        $"<p>{WebUtility.HtmlEncode(FormatVietnamTime(s.ScheduledAtUtc))}</p>" +
        $"<p>Action by {WebUtility.HtmlEncode(actor.FullName)}</p>" +
        (string.IsNullOrWhiteSpace(s.Details) ? "" : $"<p>{WebUtility.HtmlEncode(s.Details).Replace("\n", "<br>")}</p>") +
        (string.IsNullOrWhiteSpace(s.CancellationReason) ? "" : $"<p>Reason: {WebUtility.HtmlEncode(s.CancellationReason)}</p>");

    private static string NotificationTitle(ScheduleEventType type, bool self) => (self, type) switch
    {
        (true, ScheduleEventType.Created) => "Your schedule was created",
        (true, ScheduleEventType.Edited) => "Your schedule change was saved",
        (true, ScheduleEventType.Cancelled) => "Your schedule was cancelled",
        (false, ScheduleEventType.Created) => "A schedule was created",
        (false, ScheduleEventType.Edited) => "A schedule was updated",
        _ => "A schedule was cancelled"
    };

    private async Task<Schedule> LoadSchedule(Guid id, CancellationToken ct) =>
        await _context.Set<Schedule>().Include(x => x.Conversation).FirstOrDefaultAsync(x => x.ScheduleId == id, ct)
        ?? throw new NotFoundException("Schedule does not exist.");

    private Task<List<ConversationParticipant>> ActiveParticipants(Guid conversationId, CancellationToken ct) =>
        _context.Set<ConversationParticipant>().Include(x => x.User)
            .Where(x => x.ConversationsId == conversationId && x.LeftAt == null && x.DeletedAt == null).ToListAsync(ct);

    private static void EnsureClientFreelancerConversation(List<ConversationParticipant> ps, Guid actor)
    {
        EnsureParticipant(ps, actor);
        if (!ps.Any(x => x.ParticipantRole == (int)ParticipantRole.Client) || !ps.Any(x => x.ParticipantRole == (int)ParticipantRole.Freelancer))
            throw new BadRequestException("The conversation must contain a client and freelancer.");
    }

    private static void EnsureParticipant(List<ConversationParticipant> ps, Guid userId)
    { if (!ps.Any(x => x.UserId == userId)) throw new ForbiddenAccessException("You are not a participant in this conversation."); }

    private static void EnsureMutable(Schedule s, Guid userId, DateTime now, bool isEdit)
    {
        if (s.Status == ScheduleStatus.Cancelled) throw new BadRequestException("The schedule is already cancelled.");
        if (now >= s.ScheduledAtUtc) throw new BadRequestException("The scheduled time has passed.");
        if (isEdit && s.EditCount >= MaxEdits) throw new BadRequestException("This schedule has used both available edits.");
        var beforeCutoff = now < CutoffUtc(s.ScheduledAtUtc);
        var editGrace = isEdit && userId == s.CreatedByUserId && now < GraceExpiry(s) && now < s.ScheduledAtUtc;
        if (!beforeCutoff && !editGrace)
            throw new BadRequestException(isEdit
                ? "The schedule edit window has closed."
                : "Schedules cannot be cancelled less than 24 hours before their start time.");
    }

    private static ScheduleResponse ToResponse(Schedule s, Guid userId, DateTime now) => new(
        s.ScheduleId, s.ConversationId, s.CreatedByUserId, s.Title, s.Details, s.ScheduledAtUtc, s.TimeZoneId,
        (int)s.Status, s.EditCount, Math.Max(0, MaxEdits - s.EditCount), s.Version, s.CancelledByUserId,
        s.CancellationReason, s.CreatedAt, s.UpdatedAt, s.CancelledAt, CutoffUtc(s.ScheduledAtUtc), GraceExpiry(s),
        CanEdit(s, userId, now), CanCancel(s, userId, now));

    private static ScheduleEventResponse ToEvent(Schedule s, Guid messageId, ScheduleEventType type, User actor,
        string? reason) => new(1, s.ScheduleId, s.ConversationId, messageId, (int)type,
        s.Version, (int)s.Status, s.Title, s.Details, s.ScheduledAtUtc, s.TimeZoneId, actor.UserId, actor.FullName, s.CreatedByUserId,
        s.EditCount, Math.Max(0, MaxEdits - s.EditCount), s.Version, reason, CutoffUtc(s.ScheduledAtUtc),
        GraceExpiry(s), false, false);

    private static ScheduleEventResponse PersonalizeEvent(
        ScheduleEventResponse scheduleEvent,
        Schedule schedule,
        Guid viewerUserId,
        DateTime now) => scheduleEvent with
    {
        CanEdit = CanEdit(schedule, viewerUserId, now),
        CanCancel = CanCancel(schedule, viewerUserId, now)
    };

    private static bool CanEdit(Schedule s, Guid user, DateTime now) => s.Status == ScheduleStatus.Scheduled && s.EditCount < MaxEdits &&
        s.Conversation.Status == (int)ConversationStatus.Active && now < s.ScheduledAtUtc &&
        (now < CutoffUtc(s.ScheduledAtUtc) || user == s.CreatedByUserId && now < GraceExpiry(s));
    private static bool CanCancel(Schedule s, Guid user, DateTime now) => s.Status == ScheduleStatus.Scheduled && now < s.ScheduledAtUtc &&
        now < CutoffUtc(s.ScheduledAtUtc);

    private static DateTime GraceExpiry(Schedule s) => new[] { s.CreatedAt.AddMinutes(10), s.ScheduledAtUtc }.Min();
    private static DateTime CutoffUtc(DateTime utc)
        => Utc(utc).AddHours(-24);
    private static TimeZoneInfo VietnamZone()
    { try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); } catch { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); } }
    private static string FormatVietnamTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), VietnamZone()).ToString("dd MMM yyyy, HH:mm 'ICT'", CultureInfo.InvariantCulture);
    private static DateTime NormalizeInstant(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string NormalizeTitle(string value) => Whitespace.Replace((value ?? "").Normalize(NormalizationForm.FormC).Trim(), " ");
    private static string? NormalizeDetails(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }
    private static void ValidateFields(string title, string? details, DateTime start, DateTime now)
    {
        if (title.Length == 0) throw new BadRequestException("A schedule title is required.");
        if (title.Length > 200) throw new BadRequestException("Title cannot exceed 200 characters.");
        if (details?.Length > 4000) throw new BadRequestException("Details cannot exceed 4000 characters.");
        if (start <= now) throw new BadRequestException("The scheduled time must be in the future.");
    }
}

public record ScheduleDeliveryPayload(Guid NotificationId, Guid UserId, string Email, string Subject, string HtmlBody, string Metadata);
