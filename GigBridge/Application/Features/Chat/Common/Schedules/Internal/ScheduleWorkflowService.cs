using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Chat.Common.Interfaces;
using Application.Features.Notifications.Common.Interfaces;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.Notifications.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Features.Chat.Common.Schedules;

public sealed class ScheduleWorkflowService
{
    private const int MaxEdits = 2;
    private const int MaxRescheduleRequests = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IChatRealtimeNotifier _chat;
    private readonly IGoogleMeetOAuthService _meetOAuth;
    private readonly IScheduleEmailRenderer _emailRenderer;
    private readonly INotificationSender? _notificationSender;
    private readonly string _frontendBaseUrl;

    public ScheduleWorkflowService(
        IApplicationDbContext context,
        IDateTimeService clock,
        IChatRealtimeNotifier chat,
        IGoogleMeetOAuthService meetOAuth,
        IScheduleEmailRenderer emailRenderer,
        IConfiguration? configuration = null,
        INotificationSender? notificationSender = null)
    {
        _context = context;
        _clock = clock;
        _chat = chat;
        _meetOAuth = meetOAuth;
        _emailRenderer = emailRenderer;
        _notificationSender = notificationSender;
        _frontendBaseUrl = (configuration?["FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
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
        EnsureRole(participants, command.UserId, ParticipantRole.Client, "Only the client can create a schedule.");
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

        // Handle Google Meet provisioning
        GoogleMeetProvisioningJob? meetJob = null;
        if (command.Request.AddGoogleMeet)
        {
            var connection = await _context.Set<GoogleMeetConnection>()
                .Where(c => c.UserId == command.UserId && c.DisconnectedAt == null &&
                    c.Status == GoogleMeetConnectionStatus.Active)
                .FirstOrDefaultAsync(ct);

            if (connection is null)
                throw new BadRequestException(
                    "You must connect your Google account to create a meeting room. " +
                    "Go to Google Meet settings to connect.");

            meetJob = new GoogleMeetProvisioningJob
            {
                GoogleMeetProvisioningJobId = Guid.NewGuid(),
                Attempt = 1,
                Status = GoogleMeetProvisioningJobStatus.Pending,
                AttemptCount = 0,
                CreatedAt = now,
                OrganizerUserId = command.UserId
            };
        }

        var schedule = new Schedule
        {
            ScheduleId = Guid.NewGuid(),
            ConversationId = conversation.ConversationsId,
            CreatedByUserId = command.UserId,
            Title = title,
            Details = details,
            ScheduledAtUtc = startsAt,
            TimeZoneId = command.Request.TimeZoneId,
            Status = ScheduleStatus.Scheduled,
            AgreementStatus = ScheduleAgreementStatus.AwaitingFreelancer,
            EditCount = 0,
            Version = 1,
            CreatedAt = now,
            MeetingProvider = command.Request.AddGoogleMeet ? ScheduleMeetingProvider.GoogleMeet : ScheduleMeetingProvider.None,
            MeetingStatus = command.Request.AddGoogleMeet ? MeetingProvisioningStatus.Pending : MeetingProvisioningStatus.None,
            MeetingAttempt = command.Request.AddGoogleMeet ? 1 : 0
        };

        if (meetJob is not null)
        {
            meetJob.ScheduleId = schedule.ScheduleId;
            schedule.MeetProvisioningJobs.Add(meetJob);
        }

        _context.Set<Schedule>().Add(schedule);
        return await PersistEvent(schedule, conversation, participants, actor, ScheduleEventType.Created, null,
            now, ct, sendEmail: command.Request.SendEmailNotification);
    }

    public async Task<ScheduleMutationResult> Handle(UpdateScheduleCommand command, CancellationToken ct)
    {
        var now = Utc(_clock.UtcNow);
        var schedule = await LoadSchedule(command.ScheduleId, ct);
        var participants = await ActiveParticipants(schedule.ConversationId, ct);
        EnsureParticipant(participants, command.UserId);
        if (command.UserId != schedule.CreatedByUserId)
            throw new ForbiddenAccessException(
                "Freelancers must request a schedule date change for the client to approve.");
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
        // Editing retains existing meeting state - never creates a new room or replaces a pending job
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
        if (schedule.AgreementStatus == ScheduleAgreementStatus.AwaitingClientReschedule)
            ClearProposedTime(schedule);

        // Cancel any pending provisioning jobs
        var pendingJobs = await _context.Set<GoogleMeetProvisioningJob>()
            .Where(j => j.ScheduleId == schedule.ScheduleId &&
                j.Status == GoogleMeetProvisioningJobStatus.Pending)
            .ToListAsync(ct);

        foreach (var job in pendingJobs)
        {
            job.Status = GoogleMeetProvisioningJobStatus.Cancelled;
        }

        schedule.Status = ScheduleStatus.Cancelled; schedule.CancelledByUserId = command.UserId;
        schedule.CancellationReason = reason; schedule.CancelledAt = now; schedule.UpdatedAt = now; schedule.Version++;
        var actor = participants.Single(x => x.UserId == command.UserId).User;
        return await PersistEvent(schedule, schedule.Conversation, participants, actor, ScheduleEventType.Cancelled, reason, now, ct);
    }

    public async Task<ScheduleMutationResult> Handle(AcceptScheduleCommand command, CancellationToken ct)
    {
        var now = Utc(_clock.UtcNow);
        var schedule = await LoadSchedule(command.ScheduleId, ct);
        var participants = await ActiveParticipants(schedule.ConversationId, ct);
        EnsureParticipant(participants, command.UserId);
        EnsureActiveAndVersion(schedule, command.Request.ExpectedVersion, now);

        if (schedule.AgreementStatus == ScheduleAgreementStatus.AwaitingFreelancer)
            EnsureRole(participants, command.UserId, ParticipantRole.Freelancer,
                "Only the freelancer can accept the client's schedule.");
        else if (schedule.AgreementStatus is ScheduleAgreementStatus.AwaitingClient or
                 ScheduleAgreementStatus.AwaitingClientReschedule)
        {
            EnsureRole(participants, command.UserId, ParticipantRole.Client,
                "Only the client can accept the counterproposal.");
            ApplyAcceptedProposal(schedule, now);
        }
        else
            throw new BadRequestException("This schedule is not awaiting your acceptance.");

        schedule.AgreementStatus = ScheduleAgreementStatus.Accepted;
        schedule.Version++;
        schedule.UpdatedAt = now;
        var actor = participants.Single(x => x.UserId == command.UserId).User;
        return await PersistEvent(schedule, schedule.Conversation, participants, actor,
            ScheduleEventType.Accepted, null, now, ct);
    }

    public async Task<ScheduleMutationResult> Handle(RejectScheduleCommand command, CancellationToken ct)
    {
        var now = Utc(_clock.UtcNow);
        var schedule = await LoadSchedule(command.ScheduleId, ct);
        var participants = await ActiveParticipants(schedule.ConversationId, ct);
        EnsureParticipant(participants, command.UserId);
        EnsureActiveAndVersion(schedule, command.Request.ExpectedVersion, now);
        var persistedEventType = ScheduleEventType.Rejected;
        string? persistedReason = null;

        if (schedule.AgreementStatus == ScheduleAgreementStatus.AwaitingFreelancer)
        {
            EnsureRole(participants, command.UserId, ParticipantRole.Freelancer,
                "Only the freelancer can reject the client's schedule.");
            schedule.AgreementStatus = ScheduleAgreementStatus.FreelancerRejectedAwaitingCounterproposal;
        }
        else if (schedule.AgreementStatus is ScheduleAgreementStatus.AwaitingClient or
                 ScheduleAgreementStatus.AwaitingClientReschedule)
        {
            EnsureRole(participants, command.UserId, ParticipantRole.Client,
                "Only the client can reject the counterproposal.");
            var isRescheduleRequest =
                schedule.AgreementStatus == ScheduleAgreementStatus.AwaitingClientReschedule;
            ClearProposedTime(schedule);
            if (isRescheduleRequest)
            {
                schedule.RescheduleRejectionCount++;
                if (schedule.RescheduleRejectionCount >= MaxRescheduleRequests)
                {
                    persistedReason =
                        "Automatically cancelled after the client rejected three freelancer reschedule requests.";
                    schedule.AgreementStatus = ScheduleAgreementStatus.RescheduleRejected;
                    schedule.Status = ScheduleStatus.Cancelled;
                    schedule.CancellationReason = persistedReason;
                    schedule.CancelledAt = now;
                    persistedEventType = ScheduleEventType.Cancelled;
                    await CancelPendingMeetingJobs(schedule.ScheduleId, ct);
                }
                else
                {
                    schedule.AgreementStatus = ScheduleAgreementStatus.RescheduleRejected;
                }
            }
            else
            {
                schedule.AgreementStatus = ScheduleAgreementStatus.ClientRejected;
                schedule.Status = ScheduleStatus.Rejected;
                await CancelPendingMeetingJobs(schedule.ScheduleId, ct);
            }
        }
        else
            throw new BadRequestException("This schedule is not awaiting your response.");

        schedule.Version++;
        schedule.UpdatedAt = now;
        var actor = participants.Single(x => x.UserId == command.UserId).User;
        return await PersistEvent(schedule, schedule.Conversation, participants, actor,
            persistedEventType, persistedReason, now, ct);
    }

    public async Task<ScheduleMutationResult> Handle(CreateCounterProposalCommand command, CancellationToken ct)
    {
        var now = Utc(_clock.UtcNow);
        var schedule = await LoadSchedule(command.ScheduleId, ct);
        var participants = await ActiveParticipants(schedule.ConversationId, ct);
        EnsureRole(participants, command.UserId, ParticipantRole.Freelancer,
            "Only the freelancer can propose a replacement time.");
        if (schedule.Status != ScheduleStatus.Scheduled)
            throw new BadRequestException("The schedule is no longer active.");
        if (now >= schedule.ScheduledAtUtc)
            throw new BadRequestException("The scheduled time has passed.");
        if (schedule.Version != command.Request.ExpectedVersion)
            throw new ConflictException("The schedule changed. Refresh it before responding.");
        if (schedule.AgreementStatus is not (ScheduleAgreementStatus.FreelancerRejectedAwaitingCounterproposal or
            ScheduleAgreementStatus.Accepted or ScheduleAgreementStatus.RescheduleRejected))
            throw new BadRequestException("This schedule cannot receive a reschedule request right now.");
        if (schedule.RescheduleRequestCount >= MaxRescheduleRequests)
            throw new BadRequestException("The freelancer has used all three reschedule requests.");

        var isRescheduleRequest = schedule.AgreementStatus is
            ScheduleAgreementStatus.Accepted or ScheduleAgreementStatus.RescheduleRejected;
        ApplyCounterProposal(schedule, command.Request, now, isEdit: false, isRescheduleRequest);
        var actor = participants.Single(x => x.UserId == command.UserId).User;
        return await PersistEvent(schedule, schedule.Conversation, participants, actor,
            ScheduleEventType.CounterProposed, null, now, ct);
    }

    public async Task<ScheduleMutationResult> Handle(UpdateCounterProposalCommand command, CancellationToken ct)
    {
        var now = Utc(_clock.UtcNow);
        var schedule = await LoadSchedule(command.ScheduleId, ct);
        var participants = await ActiveParticipants(schedule.ConversationId, ct);
        EnsureRole(participants, command.UserId, ParticipantRole.Freelancer,
            "Only the freelancer can edit the counterproposal.");
        EnsureActiveAndVersion(schedule, command.Request.ExpectedVersion, now);
        if (schedule.AgreementStatus is not (ScheduleAgreementStatus.AwaitingClient or
            ScheduleAgreementStatus.AwaitingClientReschedule) ||
            schedule.CounterProposalCreatedAtUtc is null)
            throw new BadRequestException("There is no editable counterproposal.");
        if (now >= CounterProposalEditExpiry(schedule))
            throw new BadRequestException("The counterproposal edit window has closed.");

        ApplyCounterProposal(schedule, command.Request, now, isEdit: true,
            schedule.AgreementStatus == ScheduleAgreementStatus.AwaitingClientReschedule);
        var actor = participants.Single(x => x.UserId == command.UserId).User;
        return await PersistEvent(schedule, schedule.Conversation, participants, actor,
            ScheduleEventType.Edited, null, now, ct);
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

    public async Task<ScheduleMutationResult> Handle(RetryScheduleMeetingCommand command, CancellationToken ct)
    {
        var now = Utc(_clock.UtcNow);
        var schedule = await LoadSchedule(command.ScheduleId, ct);
        var participants = await ActiveParticipants(schedule.ConversationId, ct);
        EnsureParticipant(participants, command.UserId);

        // Only the organizer can retry
        if (schedule.CreatedByUserId != command.UserId)
            throw new ForbiddenAccessException("Only the schedule creator can retry meeting creation.");

        // Validate retry conditions
        if (schedule.Status != ScheduleStatus.Scheduled)
            throw new BadRequestException("The schedule is not active.");
        if (now >= schedule.ScheduledAtUtc)
            throw new BadRequestException("The scheduled time has passed.");
        if (schedule.MeetingProvider != ScheduleMeetingProvider.GoogleMeet)
            throw new BadRequestException("This schedule does not have a Google Meet room.");
        if (schedule.MeetingStatus == MeetingProvisioningStatus.Ready)
            throw new BadRequestException("The meeting room is already ready.");
        if (schedule.MeetingStatus == MeetingProvisioningStatus.Pending)
            throw new ConflictException("A meeting room is already being created.");

        // Verify the latest job for current attempt is Failed or Ambiguous
        var latestJob = await _context.Set<GoogleMeetProvisioningJob>()
            .Where(j => j.ScheduleId == schedule.ScheduleId && j.Attempt == schedule.MeetingAttempt)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (latestJob is null ||
            (latestJob.Status != GoogleMeetProvisioningJobStatus.Failed &&
             latestJob.Status != GoogleMeetProvisioningJobStatus.Ambiguous))
            throw new ConflictException("No failed meeting job to retry.");

        // Verify Google connection is active
        var connection = await _context.Set<GoogleMeetConnection>()
            .Where(c => c.UserId == command.UserId && c.DisconnectedAt == null &&
                c.Status == GoogleMeetConnectionStatus.Active)
            .FirstOrDefaultAsync(ct);

        if (connection is null)
            throw new BadRequestException("Your Google account must be connected to retry.");

        // Increment meeting attempt and create new job
        schedule.MeetingAttempt++;
        schedule.MeetingStatus = MeetingProvisioningStatus.Pending;
        schedule.MeetingFailureCode = null;
        schedule.MeetingLastAttemptAt = now;

        var newJob = new GoogleMeetProvisioningJob
        {
            GoogleMeetProvisioningJobId = Guid.NewGuid(),
            ScheduleId = schedule.ScheduleId,
            OrganizerUserId = command.UserId,
            Attempt = schedule.MeetingAttempt,
            Status = GoogleMeetProvisioningJobStatus.Pending,
            AttemptCount = 0,
            CreatedAt = now
        };

        _context.Set<GoogleMeetProvisioningJob>().Add(newJob);

        var actor = participants.Single(x => x.UserId == command.UserId).User;

        // Persist and send realtime update for the retry
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new ConflictException("The schedule was changed by the other participant.", ex); }
        catch (DbUpdateException ex) { throw new ConflictException("A concurrent schedule event was already persisted. Refresh and retry.", ex); }

        // Send realtime meeting update
        var meeting = ToMeetingResponse(schedule, command.UserId, now);
        foreach (var participant in participants.GroupBy(x => x.UserId).Select(x => x.First()))
        {
            var viewerMeeting = meeting! with { CanRetry = participant.UserId == schedule.CreatedByUserId };
            await _chat.SendUserEventAsync(participant.UserId, "ScheduleMeetingChanged", new
            {
                conversationId = schedule.ConversationId,
                scheduleId = schedule.ScheduleId,
                meeting = viewerMeeting
            }, ct);
        }

        return new ScheduleMutationResult(
            ToResponse(schedule, command.UserId, now),
            null!); // Retry doesn't create a chat message
    }

    private async Task<ScheduleMutationResult> PersistEvent(Schedule schedule, Conversation conversation,
        List<ConversationParticipant> participants, User actor, ScheduleEventType eventType, string? reason,
        DateTime now, CancellationToken ct, bool sendEmail = true)
    {
        var realtimeNotifications = new List<(Guid UserId, Notification Notification)>();
        var messageContent = eventType switch
        {
            ScheduleEventType.Created => $"Scheduled: {schedule.Title}",
            ScheduleEventType.Edited => $"Schedule updated: {schedule.Title}",
            ScheduleEventType.Cancelled => $"Schedule cancelled: {schedule.Title}",
            ScheduleEventType.Accepted => $"Schedule accepted: {schedule.Title}",
            ScheduleEventType.Rejected => schedule.AgreementStatus is
                ScheduleAgreementStatus.ClientRejected or ScheduleAgreementStatus.RescheduleRejected
                ? $"Counterproposal rejected: {schedule.Title}"
                : $"Schedule rejected: {schedule.Title}",
            _ => $"New schedule time proposed: {schedule.Title}"
        };

        // A schedule owns one chat card. State transitions update that message
        // instead of appending another card for the same schedule.
        var message = eventType == ScheduleEventType.Created
            ? null
            : await _context.Set<Message>()
                .Where(x => x.ScheduleId == schedule.ScheduleId && x.MessageType == (int)MessageType.Schedule)
                .OrderByDescending(x => x.ScheduleEventSequence)
                .FirstOrDefaultAsync(ct);

        if (message is null)
        {
            message = new Message
            {
                MessagesId = Guid.NewGuid(),
                ConversationsId = conversation.ConversationsId,
                SenderUserId = actor.UserId,
                MessageType = (int)MessageType.Schedule,
                ScheduleId = schedule.ScheduleId,
                SentAt = now
            };
            _context.Set<Message>().Add(message);
        }

        message.Content = messageContent;
        message.ScheduleEventType = eventType;
        message.ScheduleEventSequence = schedule.Version;
        if (eventType != ScheduleEventType.Created)
        {
            // The card represents the latest schedule event, so move it to the
            // event's chronological position instead of leaving it at the
            // original creation position near the top of the conversation.
            message.SentAt = now;
            message.EditedAt = now;
            message.SenderUserId = actor.UserId;
        }

        var eventDto = ToEvent(schedule, message.MessagesId, eventType, actor, reason);
        message.Metadata = JsonSerializer.Serialize(eventDto, JsonOptions);

        conversation.LastMessageId = message.MessagesId;
        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;

        foreach (var p in participants.Where(x => x.UserId != actor.UserId)
            .GroupBy(x => x.UserId).Select(x => x.First()))
            p.UnreadCount++;

        foreach (var participant in participants.GroupBy(x => x.UserId).Select(x => x.First()))
        {
            var snapshot = PersonalizeEvent(eventDto, schedule, participant.UserId, now);
            var metadata = JsonSerializer.Serialize(snapshot, JsonOptions);
            var existing = await _context.Set<Notification>().FirstOrDefaultAsync(n =>
                n.UserId == participant.UserId && n.Type == (int)NotificationType.Schedule &&
                n.ReferenceId == schedule.ScheduleId && n.IsRead != true, ct);
            var self = participant.UserId == actor.UserId;
            var title = NotificationTitle(eventType, schedule.AgreementStatus, self,
                actor.UserId == schedule.CreatedByUserId);
            var content = $"{actor.FullName}: {schedule.Title} — {FormatVietnamTime(EventTime(schedule))}";
            var notification = existing ?? new Notification { NotificationsId = Guid.NewGuid(), UserId = participant.UserId };
            if (existing is null) _context.Set<Notification>().Add(notification);
            if ((notification.Revision ?? 0) < schedule.Version)
            {
                notification.Type = (int)NotificationType.Schedule;
                notification.Title = title;
                notification.Content = content;
                notification.ReferenceId = schedule.ScheduleId;
                notification.ReferenceType = "Schedule";
                notification.Metadata = metadata;
                notification.Revision = schedule.Version;
                notification.IsRead = false;
                notification.ReadAt = null;
                notification.CreatedAt = now;
            }

            realtimeNotifications.Add((participant.UserId, notification));
            AddEmailOutbox(schedule, participant.UserId, notification.NotificationsId, eventType, actor,
                participant.User, metadata, now, snapshot.ScheduleMessageId, sendEmail);
        }

        await SyncMeetingStartDeliveries(schedule, participants, eventDto, now, ct);

        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new ConflictException("The schedule was changed by the other participant.", ex); }
        catch (DbUpdateException ex) { throw new ConflictException("A concurrent schedule event was already persisted. Refresh and retry.", ex); }

        if (_notificationSender is not null)
        {
            await Task.WhenAll(realtimeNotifications.Select(item =>
                _notificationSender.SendToUserAsync(
                    item.UserId,
                    ToNotificationDto(item.Notification),
                    ct)));
        }

        // Send realtime events
        MessageResponse? actorMessageResponse = null;
        foreach (var participant in participants.GroupBy(x => x.UserId).Select(x => x.First()))
        {
            var snapshot = PersonalizeEvent(eventDto, schedule, participant.UserId, now);
            var metadata = JsonSerializer.Serialize(snapshot, JsonOptions);
            var meeting = ToMeetingResponse(schedule, participant.UserId, now);
            var messageResponse = new MessageResponse(
                message.MessagesId, message.ConversationsId, message.SenderUserId,
                message.MessageType, message.Content, null, metadata, null, message.SentAt, message.EditedAt, false, [], snapshot);

            // Send meeting event if this schedule has a meeting
            if (schedule.MeetingProvider != ScheduleMeetingProvider.None && eventType == ScheduleEventType.Created)
            {
                await _chat.SendUserEventAsync(participant.UserId, "ScheduleMeetingChanged", new
                {
                    conversationId = schedule.ConversationId,
                    scheduleId = schedule.ScheduleId,
                    meeting
                }, ct);
            }

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

    private void AddEmailOutbox(Schedule schedule, Guid recipientId, Guid notificationId, ScheduleEventType type,
        User actor, User recipient, string metadata, DateTime now, Guid scheduleMessageId, bool sendEmail)
    {
        if (!sendEmail)
        {
            return;
        }

        var email = _emailRenderer.Render(ResolveNotificationType(type, schedule.AgreementStatus),
            new ScheduleEmailModel(recipient.FullName, actor.FullName, recipient.UserId == actor.UserId,
                schedule.Title, FormatVietnamTime(EventTime(schedule)), schedule.Details,
                schedule.CancellationReason, BuildScheduleUrl(schedule.ConversationId, scheduleMessageId)));
        var payload = JsonSerializer.Serialize(new ScheduleDeliveryPayload(notificationId, recipientId, recipient.Email,
            email.Subject, email.HtmlBody, metadata, TextBody: email.TextBody), JsonOptions);
        _context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
        {
            DeliveryOutboxId = Guid.NewGuid(),
            DeliveryKey = $"schedule:{schedule.ScheduleId}:{schedule.Version}:{recipientId}:{(int)DeliveryChannel.Email}",
            ScheduleId = schedule.ScheduleId,
            RecipientUserId = recipientId,
            EventSequence = schedule.Version,
            Channel = (int)DeliveryChannel.Email,
            Payload = payload,
            Status = (int)DeliveryOutboxStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now
        });
    }

    private static NotificationDto ToNotificationDto(Notification notification) => new()
    {
        Id = notification.NotificationsId,
        Source = "Personal",
        NotificationId = notification.NotificationsId,
        ReadTargetId = notification.NotificationsId,
        Type = (NotificationType)notification.Type,
        Title = notification.Title,
        Content = notification.Content,
        ReferenceId = notification.ReferenceId,
        ReferenceType = notification.ReferenceType,
        Metadata = notification.Metadata,
        Revision = notification.Revision,
        IsRead = notification.IsRead ?? false,
        ReadAt = notification.ReadAt,
        CreatedAt = notification.CreatedAt
    };

    private async Task SyncMeetingStartDeliveries(Schedule schedule,
        List<ConversationParticipant> participants, ScheduleEventResponse eventDto, DateTime now,
        CancellationToken ct)
    {
        var existing = await _context.Set<DeliveryOutbox>()
            .Where(x => x.ScheduleId == schedule.ScheduleId && x.DeliveryKey.Contains(":start:"))
            .ToListAsync(ct);
        var shouldDeliver = schedule.Status == ScheduleStatus.Scheduled &&
            schedule.AgreementStatus is (ScheduleAgreementStatus.Accepted or
                ScheduleAgreementStatus.AwaitingClientReschedule or ScheduleAgreementStatus.RescheduleRejected) &&
            schedule.ScheduledAtUtc > now;

        if (!shouldDeliver)
        {
            foreach (var job in existing.Where(x => x.Status is (int)DeliveryOutboxStatus.Pending or
                         (int)DeliveryOutboxStatus.Processing))
            {
                job.Status = (int)DeliveryOutboxStatus.Cancelled;
                job.ClaimToken = null;
                job.LastError = "Schedule is no longer awaiting its start time.";
            }
            return;
        }

        foreach (var participant in participants.GroupBy(x => x.UserId).Select(x => x.First()))
        {
            var snapshot = PersonalizeEvent(eventDto, schedule, participant.UserId, now);
            var metadata = JsonSerializer.Serialize(snapshot, JsonOptions);
            var realtimeKey = StartDeliveryKey(schedule.ScheduleId, participant.UserId,
                DeliveryChannel.NotificationRealtime);
            var existingRealtime = existing.FirstOrDefault(x => x.DeliveryKey == realtimeKey);
            var notificationId = TryGetNotificationId(existingRealtime?.Payload) ?? Guid.NewGuid();
            var title = "Meeting time reached";
            var content = $"{schedule.Title} is starting now — {FormatVietnamTime(schedule.ScheduledAtUtc)}";
            var email = _emailRenderer.Render(ScheduleNotificationType.MeetingStarting,
                new ScheduleEmailModel(participant.User.FullName, "GigBridge", false,
                    schedule.Title, FormatVietnamTime(schedule.ScheduledAtUtc), schedule.Details, null,
                    BuildScheduleUrl(schedule.ConversationId, eventDto.ScheduleMessageId),
                    schedule.MeetingStatus == MeetingProvisioningStatus.Ready ? schedule.MeetingJoinUri : null));

            foreach (var channel in new[] { DeliveryChannel.NotificationRealtime, DeliveryChannel.Email })
            {
                var key = StartDeliveryKey(schedule.ScheduleId, participant.UserId, channel);
                var job = existing.FirstOrDefault(x => x.DeliveryKey == key);
                var payload = JsonSerializer.Serialize(new ScheduleDeliveryPayload(
                    notificationId, participant.UserId, participant.User.Email,
                    email.Subject, email.HtmlBody, metadata,
                    true, title, content, schedule.ScheduleId, schedule.Version, email.TextBody), JsonOptions);
                if (job is null)
                {
                    _context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
                    {
                        DeliveryOutboxId = Guid.NewGuid(), DeliveryKey = key,
                        ScheduleId = schedule.ScheduleId, RecipientUserId = participant.UserId,
                        EventSequence = schedule.Version, Channel = (int)channel, Payload = payload,
                        Status = (int)DeliveryOutboxStatus.Pending, NextAttemptAt = schedule.ScheduledAtUtc,
                        CreatedAt = now
                    });
                }
                else if (job.Status is (int)DeliveryOutboxStatus.Pending or (int)DeliveryOutboxStatus.Processing)
                {
                    job.EventSequence = schedule.Version;
                    job.Payload = payload;
                    job.Status = (int)DeliveryOutboxStatus.Pending;
                    job.ClaimToken = null;
                    job.AttemptCount = 0;
                    job.NextAttemptAt = schedule.ScheduledAtUtc;
                    job.LastError = null;
                }
            }
        }
    }

    private static string StartDeliveryKey(Guid scheduleId, Guid userId, DeliveryChannel channel) =>
        $"schedule:{scheduleId}:start:{userId}:{(int)channel}";

    private static Guid? TryGetNotificationId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try { return JsonSerializer.Deserialize<ScheduleDeliveryPayload>(payload, JsonOptions)?.NotificationId; }
        catch { return null; }
    }

    private static ScheduleNotificationType ResolveNotificationType(
        ScheduleEventType type, ScheduleAgreementStatus agreement) => type switch
    {
        ScheduleEventType.Created => ScheduleNotificationType.ProposalCreated,
        ScheduleEventType.Edited when agreement is ScheduleAgreementStatus.AwaitingClient or
            ScheduleAgreementStatus.AwaitingClientReschedule => ScheduleNotificationType.CounterProposalUpdated,
        ScheduleEventType.Edited => ScheduleNotificationType.ScheduleUpdated,
        ScheduleEventType.Cancelled => ScheduleNotificationType.ScheduleCancelled,
        ScheduleEventType.Accepted => ScheduleNotificationType.ScheduleConfirmed,
        ScheduleEventType.CounterProposed => ScheduleNotificationType.CounterProposalCreated,
        ScheduleEventType.Rejected when agreement is ScheduleAgreementStatus.ClientRejected or
            ScheduleAgreementStatus.RescheduleRejected => ScheduleNotificationType.CounterProposalDeclined,
        ScheduleEventType.Rejected => ScheduleNotificationType.ScheduleDeclined,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private string BuildScheduleUrl(Guid conversationId, Guid messageId) =>
        $"{_frontendBaseUrl}/messages?conversationId={conversationId:D}&messageId={messageId:D}";

    private static string NotificationTitle(ScheduleEventType type, ScheduleAgreementStatus agreement,
        bool self, bool actorIsCreator) => (self, type, agreement, actorIsCreator) switch
    {
        (true, ScheduleEventType.Created, _, _) => "Your schedule was created",
        (true, ScheduleEventType.Edited, _, _) => "Your schedule change was saved",
        (true, ScheduleEventType.Cancelled, _, _) => "Your schedule was cancelled",
        (false, ScheduleEventType.Created, _, _) => "A schedule needs your response",
        (false, ScheduleEventType.Edited, _, _) => "A schedule was updated",
        (false, ScheduleEventType.Rejected, ScheduleAgreementStatus.FreelancerRejectedAwaitingCounterproposal, _) => "Freelancer rejected the schedule",
        (false, ScheduleEventType.CounterProposed, _, _) => "Freelancer proposed a new schedule time",
        (false, ScheduleEventType.Accepted, _, true) => "Client accepted the proposed schedule",
        (false, ScheduleEventType.Accepted, _, false) => "Freelancer accepted the schedule",
        (false, ScheduleEventType.Rejected, ScheduleAgreementStatus.ClientRejected, _) => "Client rejected the proposed schedule",
        (false, ScheduleEventType.Rejected, ScheduleAgreementStatus.RescheduleRejected, _) => "Client rejected the schedule change",
        (true, ScheduleEventType.CounterProposed, _, _) => "Your new schedule time was sent",
        (true, ScheduleEventType.Accepted, _, _) => "You accepted the schedule",
        (true, ScheduleEventType.Rejected, _, _) => "You rejected the schedule",
        _ => "A schedule was cancelled"
    };

    private async Task<Schedule> LoadSchedule(Guid id, CancellationToken ct) =>
        await _context.Set<Schedule>().Include(x => x.Conversation).FirstOrDefaultAsync(x => x.ScheduleId == id, ct)
        ?? throw new NotFoundException("Schedule does not exist.");

    private Task<List<ConversationParticipant>> ActiveParticipants(Guid conversationId, CancellationToken ct) =>
        _context.Set<ConversationParticipant>().Include(x => x.User)
            .Where(x => x.ConversationsId == conversationId && x.LeftAt == null && x.DeletedAt == null)
            .ToListAsync(ct);

    private static void EnsureClientFreelancerConversation(List<ConversationParticipant> ps, Guid actor)
    {
        EnsureParticipant(ps, actor);
        if (!ps.Any(x => x.ParticipantRole == (int)ParticipantRole.Client) ||
            !ps.Any(x => x.ParticipantRole == (int)ParticipantRole.Freelancer))
            throw new BadRequestException("The conversation must contain a client and freelancer.");
    }

    private static void EnsureParticipant(List<ConversationParticipant> ps, Guid userId)
    {
        if (!ps.Any(x => x.UserId == userId))
            throw new ForbiddenAccessException("You are not a participant in this conversation.");
    }

    private static void EnsureRole(List<ConversationParticipant> participants, Guid userId,
        ParticipantRole role, string message)
    {
        EnsureParticipant(participants, userId);
        if (!participants.Any(x => x.UserId == userId && x.ParticipantRole == (int)role))
            throw new ForbiddenAccessException(message);
    }

    private static void EnsureActiveAndVersion(Schedule schedule, int expectedVersion, DateTime now)
    {
        if (schedule.Status != ScheduleStatus.Scheduled)
            throw new BadRequestException("The schedule is no longer active.");
        if (now >= ResponseDeadline(schedule))
            throw new BadRequestException("The schedule response deadline has passed.");
        if (schedule.Version != expectedVersion)
            throw new ConflictException("The schedule changed. Refresh it before responding.");
    }

    private static void ApplyCounterProposal(Schedule schedule, CounterProposalRequest request,
        DateTime now, bool isEdit, bool isRescheduleRequest)
    {
        var startsAt = NormalizeInstant(request.ScheduledAt);
        if (startsAt <= now) throw new BadRequestException("The proposed time must be in the future.");
        if (isEdit && startsAt == schedule.ProposedScheduledAtUtc &&
            request.TimeZoneId == schedule.ProposedTimeZoneId)
            throw new BadRequestException("The update does not change the proposed time.");

        schedule.ProposedScheduledAtUtc = startsAt;
        schedule.ProposedTimeZoneId = request.TimeZoneId;
        schedule.AgreementStatus = isRescheduleRequest
            ? ScheduleAgreementStatus.AwaitingClientReschedule
            : ScheduleAgreementStatus.AwaitingClient;
        if (!isEdit)
        {
            schedule.CounterProposalCreatedAtUtc = now;
            if (isRescheduleRequest) schedule.RescheduleRequestCount++;
        }
        schedule.Version++;
        schedule.UpdatedAt = now;
    }

    private static void ApplyAcceptedProposal(Schedule schedule, DateTime now)
    {
        if (schedule.ProposedScheduledAtUtc is null)
            throw new BadRequestException("The proposed schedule time is missing.");
        if (schedule.ProposedScheduledAtUtc.Value <= now)
            throw new BadRequestException("The proposed schedule time has passed.");

        schedule.ScheduledAtUtc = schedule.ProposedScheduledAtUtc.Value;
        if (!string.IsNullOrWhiteSpace(schedule.ProposedTimeZoneId))
            schedule.TimeZoneId = schedule.ProposedTimeZoneId;
        ClearProposedTime(schedule);
    }

    private static void ClearProposedTime(Schedule schedule)
    {
        schedule.ProposedScheduledAtUtc = null;
        schedule.ProposedTimeZoneId = null;
        schedule.CounterProposalCreatedAtUtc = null;
    }

    private static DateTime EventTime(Schedule schedule) =>
        schedule.ProposedScheduledAtUtc ?? schedule.ScheduledAtUtc;

    private static DateTime ResponseDeadline(Schedule schedule) =>
        schedule.AgreementStatus is (ScheduleAgreementStatus.AwaitingClient or
            ScheduleAgreementStatus.AwaitingClientReschedule) &&
        schedule.ProposedScheduledAtUtc is not null
            ? Utc(schedule.ProposedScheduledAtUtc.Value)
            : Utc(schedule.ScheduledAtUtc);

    private async Task CancelPendingMeetingJobs(Guid scheduleId, CancellationToken ct)
    {
        var pendingJobs = await _context.Set<GoogleMeetProvisioningJob>()
            .Where(j => j.ScheduleId == scheduleId && j.Status == GoogleMeetProvisioningJobStatus.Pending)
            .ToListAsync(ct);
        foreach (var job in pendingJobs) job.Status = GoogleMeetProvisioningJobStatus.Cancelled;
    }

    private static void EnsureMutable(Schedule s, Guid userId, DateTime now, bool isEdit)
    {
        if (s.Status != ScheduleStatus.Scheduled) throw new BadRequestException("The schedule is no longer active.");
        if (s.AgreementStatus is ScheduleAgreementStatus.FreelancerRejectedAwaitingCounterproposal or
                ScheduleAgreementStatus.AwaitingClient or ScheduleAgreementStatus.ClientRejected ||
            (isEdit && s.AgreementStatus == ScheduleAgreementStatus.AwaitingClientReschedule))
            throw new BadRequestException("Use the counterproposal actions for this schedule.");
        if (s.AgreementStatus == ScheduleAgreementStatus.AwaitingFreelancer && userId != s.CreatedByUserId)
            throw new ForbiddenAccessException("Only the client can change a schedule awaiting freelancer response.");
        if (now >= s.ScheduledAtUtc) throw new BadRequestException("The scheduled time has passed.");
        if (isEdit && s.EditCount >= MaxEdits) throw new BadRequestException("This schedule has used both available edits.");
        var beforeCutoff = now < CutoffUtc(s.ScheduledAtUtc);
        var editGrace = !WasCreatedMoreThan24HoursBeforeStart(s) &&
            userId == s.CreatedByUserId && now < GraceExpiry(s) && now < s.ScheduledAtUtc;
        if (!beforeCutoff && !editGrace)
            throw new BadRequestException(isEdit
                ? "The schedule edit window has closed."
                : "Schedules cannot be cancelled less than 24 hours before their start time.");
    }

    private static ScheduleResponse ToResponse(Schedule s, Guid userId, DateTime now)
    {
        return new ScheduleResponse(
            s.ScheduleId, s.ConversationId, s.CreatedByUserId, s.Title, s.Details, s.ScheduledAtUtc, s.TimeZoneId,
            (int)s.Status, s.EditCount, Math.Max(0, MaxEdits - s.EditCount), s.Version, s.CancelledByUserId,
            s.CancellationReason, s.CreatedAt, s.UpdatedAt, s.CancelledAt, CutoffUtc(s.ScheduledAtUtc), GraceExpiry(s),
            CanEdit(s, userId, now), CanCancel(s, userId, now),
            (int)s.AgreementStatus, s.CounterProposalCreatedAtUtc,
            s.CounterProposalCreatedAtUtc is null ? null : CounterProposalEditExpiry(s),
            s.ProposedScheduledAtUtc, s.ProposedTimeZoneId,
            s.RescheduleRequestCount, Math.Max(0, MaxRescheduleRequests - s.RescheduleRequestCount),
            CanAccept(s, userId, now), CanReject(s, userId, now), CanProposeTime(s, userId, now),
            CanEditCounterProposal(s, userId, now),
            ToMeetingResponse(s, userId, now));
    }

    private static ScheduleEventResponse ToEvent(Schedule s, Guid messageId, ScheduleEventType type, User actor,
        string? reason)
    {
        var meeting = type == ScheduleEventType.Created && s.MeetingProvider != ScheduleMeetingProvider.None
            ? new ScheduleMeetingResponse(
                (int)s.MeetingProvider,
                (int)s.MeetingStatus,
                s.CreatedByUserId,
                null,
                s.MeetingFailureCode,
                false)
            : null;

        return new ScheduleEventResponse(
            4, s.ScheduleId, s.ConversationId, messageId, (int)type,
            s.Version, (int)s.Status, s.Title, s.Details, s.ScheduledAtUtc, s.TimeZoneId,
            actor.UserId, actor.FullName, s.CreatedByUserId,
            s.EditCount, Math.Max(0, MaxEdits - s.EditCount), s.Version,
            s.CreatedAt, reason, CutoffUtc(s.ScheduledAtUtc), GraceExpiry(s), false, false,
            (int)s.AgreementStatus, s.CounterProposalCreatedAtUtc,
            s.CounterProposalCreatedAtUtc is null ? null : CounterProposalEditExpiry(s),
            false, false, false, false,
            meeting, s.ProposedScheduledAtUtc, s.ProposedTimeZoneId,
            s.RescheduleRequestCount, Math.Max(0, MaxRescheduleRequests - s.RescheduleRequestCount));
    }

    private static ScheduleEventResponse PersonalizeEvent(
        ScheduleEventResponse scheduleEvent,
        Schedule schedule,
        Guid viewerUserId,
        DateTime now)
    {
        var meeting = ToMeetingResponse(schedule, viewerUserId, now) ?? scheduleEvent.Meeting;
        var canRetry = meeting is not null && viewerUserId == schedule.CreatedByUserId &&
            schedule.MeetingProvider == ScheduleMeetingProvider.GoogleMeet &&
            schedule.MeetingStatus == MeetingProvisioningStatus.Failed &&
            schedule.Status == ScheduleStatus.Scheduled &&
            now < schedule.ScheduledAtUtc;

        var personalizedMeeting = meeting is null
            ? null
            : meeting with { CanRetry = canRetry };

        return scheduleEvent with
        {
            CanEdit = CanEdit(schedule, viewerUserId, now),
            CanCancel = CanCancel(schedule, viewerUserId, now),
            AgreementStatus = (int)schedule.AgreementStatus,
            CounterProposalCreatedAtUtc = schedule.CounterProposalCreatedAtUtc,
            CounterProposalEditExpiresAtUtc = schedule.CounterProposalCreatedAtUtc is null
                ? null : CounterProposalEditExpiry(schedule),
            ProposedScheduledAtUtc = schedule.ProposedScheduledAtUtc,
            ProposedTimeZoneId = schedule.ProposedTimeZoneId,
            RescheduleRequestCount = schedule.RescheduleRequestCount,
            RemainingRescheduleRequests =
                Math.Max(0, MaxRescheduleRequests - schedule.RescheduleRequestCount),
            CanAccept = CanAccept(schedule, viewerUserId, now),
            CanReject = CanReject(schedule, viewerUserId, now),
            CanProposeTime = CanProposeTime(schedule, viewerUserId, now),
            CanEditCounterProposal = CanEditCounterProposal(schedule, viewerUserId, now),
            Meeting = personalizedMeeting
        };
    }

    private static ScheduleMeetingResponse? ToMeetingResponse(Schedule s, Guid userId, DateTime now)
    {
        if (s.MeetingProvider == ScheduleMeetingProvider.None)
            return null;

        var canRetry = userId == s.CreatedByUserId &&
            s.MeetingProvider == ScheduleMeetingProvider.GoogleMeet &&
            s.MeetingStatus == MeetingProvisioningStatus.Failed &&
            s.Status == ScheduleStatus.Scheduled &&
            now < s.ScheduledAtUtc;

        return new ScheduleMeetingResponse(
            (int)s.MeetingProvider,
            (int)s.MeetingStatus,
            s.CreatedByUserId,
            s.MeetingStatus == MeetingProvisioningStatus.Ready ? s.MeetingJoinUri : null,
            s.MeetingFailureCode,
            canRetry);
    }

    private static bool CanEdit(Schedule s, Guid user, DateTime now) =>
        s.Status == ScheduleStatus.Scheduled && s.EditCount < MaxEdits &&
        user == s.CreatedByUserId &&
        s.AgreementStatus is (ScheduleAgreementStatus.Accepted or ScheduleAgreementStatus.RescheduleRejected or
            ScheduleAgreementStatus.AwaitingFreelancer) &&
        (s.AgreementStatus != ScheduleAgreementStatus.AwaitingFreelancer || user == s.CreatedByUserId) &&
        s.Conversation.Status == (int)ConversationStatus.Active && now < s.ScheduledAtUtc &&
        (now < CutoffUtc(s.ScheduledAtUtc) ||
            !WasCreatedMoreThan24HoursBeforeStart(s) && user == s.CreatedByUserId && now < GraceExpiry(s));

    private static bool CanCancel(Schedule s, Guid user, DateTime now) =>
        s.Status == ScheduleStatus.Scheduled && now < s.ScheduledAtUtc &&
        s.AgreementStatus is (ScheduleAgreementStatus.Accepted or ScheduleAgreementStatus.RescheduleRejected or
            ScheduleAgreementStatus.AwaitingClientReschedule or ScheduleAgreementStatus.AwaitingFreelancer) &&
        (s.AgreementStatus != ScheduleAgreementStatus.AwaitingFreelancer || user == s.CreatedByUserId) &&
        (now < CutoffUtc(s.ScheduledAtUtc) ||
         !WasCreatedMoreThan24HoursBeforeStart(s) && user == s.CreatedByUserId && now < GraceExpiry(s));

    private static bool CanAccept(Schedule s, Guid user, DateTime now) =>
        s.Status == ScheduleStatus.Scheduled && now < ResponseDeadline(s) &&
        (s.AgreementStatus == ScheduleAgreementStatus.AwaitingFreelancer && user != s.CreatedByUserId ||
         s.AgreementStatus is (ScheduleAgreementStatus.AwaitingClient or
             ScheduleAgreementStatus.AwaitingClientReschedule) && user == s.CreatedByUserId &&
             s.ProposedScheduledAtUtc is not null && now < s.ProposedScheduledAtUtc.Value);

    private static bool CanReject(Schedule s, Guid user, DateTime now) =>
        s.Status == ScheduleStatus.Scheduled && now < ResponseDeadline(s) &&
        (s.AgreementStatus == ScheduleAgreementStatus.AwaitingFreelancer && user != s.CreatedByUserId ||
         s.AgreementStatus is (ScheduleAgreementStatus.AwaitingClient or
             ScheduleAgreementStatus.AwaitingClientReschedule) && user == s.CreatedByUserId);

    private static bool CanProposeTime(Schedule s, Guid user, DateTime now) =>
        s.Status == ScheduleStatus.Scheduled &&
        s.RescheduleRequestCount < MaxRescheduleRequests && now < s.ScheduledAtUtc &&
        s.AgreementStatus is (ScheduleAgreementStatus.FreelancerRejectedAwaitingCounterproposal or
            ScheduleAgreementStatus.Accepted or ScheduleAgreementStatus.RescheduleRejected) &&
        user != s.CreatedByUserId;

    private static bool CanEditCounterProposal(Schedule s, Guid user, DateTime now) =>
        s.Status == ScheduleStatus.Scheduled &&
        s.AgreementStatus is (ScheduleAgreementStatus.AwaitingClient or
            ScheduleAgreementStatus.AwaitingClientReschedule) &&
        user != s.CreatedByUserId && s.CounterProposalCreatedAtUtc is not null &&
        now < CounterProposalEditExpiry(s);

    private static DateTime CounterProposalEditExpiry(Schedule s) =>
        new[]
        {
            Utc(s.CounterProposalCreatedAtUtc!.Value).AddHours(24),
            Utc(s.ProposedScheduledAtUtc ?? s.ScheduledAtUtc)
        }.Min();

    private static DateTime GraceExpiry(Schedule s) =>
        new[] { s.CreatedAt.AddMinutes(10), s.ScheduledAtUtc }.Min();

    private static bool WasCreatedMoreThan24HoursBeforeStart(Schedule s) =>
        Utc(s.CreatedAt) < CutoffUtc(s.ScheduledAtUtc);

    private static DateTime CutoffUtc(DateTime utc) =>
        Utc(utc).AddHours(-24);

    private static TimeZoneInfo VietnamZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }

    private static string FormatVietnamTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), VietnamZone())
            .ToString("dd MMM yyyy, HH:mm 'ICT'", CultureInfo.InvariantCulture);

    private static DateTime NormalizeInstant(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static string NormalizeTitle(string value) =>
        Whitespace.Replace((value ?? "").Normalize(NormalizationForm.FormC).Trim(), " ");

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

public record ScheduleDeliveryPayload(
    Guid NotificationId, Guid UserId, string Email, string Subject, string HtmlBody, string Metadata,
    bool CreateNotificationAtDelivery = false, string? NotificationTitle = null,
    string? NotificationContent = null, Guid? ReferenceId = null, int? Revision = null,
    string? TextBody = null);
