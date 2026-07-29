using System.Text.Json;
using Application.Features.Chat.Common.Schedules;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Chat.Common.Messages;

public static class MessageHelpers
{
    private const int MaxRescheduleRequests = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ScheduleEventResponse? ParseScheduleMetadata(
        Message message,
        Guid viewerUserId,
        DateTime utcNow,
        Schedule? currentSchedule = null)
    {
        if (message.MessageType != (int)MessageType.Schedule || string.IsNullOrWhiteSpace(message.Metadata))
        {
            return null;
        }

        try
        {
            var schedule = JsonSerializer.Deserialize<ScheduleEventResponse>(message.Metadata, JsonOptions);
            if (schedule is null)
            {
                return null;
            }

            var now = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
            var status = currentSchedule?.Status ?? (ScheduleStatus)schedule.Status;
            var agreement = currentSchedule?.AgreementStatus ??
                (ScheduleAgreementStatus)schedule.AgreementStatus;
            var scheduledAt = currentSchedule?.ScheduledAtUtc ?? schedule.ScheduledAtUtc;
            var createdAt = currentSchedule?.CreatedAt ?? schedule.CreatedAt;
            var createdBy = currentSchedule?.CreatedByUserId ?? schedule.CreatedByUserId;
            var editCount = currentSchedule?.EditCount ?? schedule.EditCount;
            var remainingEdits = Math.Max(0, 2 - editCount);
            var counterCreated = currentSchedule?.CounterProposalCreatedAtUtc ??
                schedule.CounterProposalCreatedAtUtc;
            var proposedAt = currentSchedule?.ProposedScheduledAtUtc ??
                schedule.ProposedScheduledAtUtc;
            var proposedTimeZoneId = currentSchedule?.ProposedTimeZoneId ??
                schedule.ProposedTimeZoneId;
            var rescheduleRequestCount = currentSchedule?.RescheduleRequestCount ??
                schedule.RescheduleRequestCount;
            var remainingRescheduleRequests =
                Math.Max(0, MaxRescheduleRequests - rescheduleRequestCount);
            var cutoff = scheduledAt.AddHours(-24);
            var graceExpiry = new[] { createdAt.AddMinutes(10), scheduledAt }.Min();
            var counterEditExpiry = counterCreated is null
                ? (DateTime?)null
                : new[] { counterCreated.Value.AddHours(24), proposedAt ?? scheduledAt }.Min();
            var isCreator = viewerUserId == createdBy;
            var isScheduled = status == ScheduleStatus.Scheduled;
            var hasNotStarted = now < scheduledAt;
            var responseDeadline =
                agreement is (ScheduleAgreementStatus.AwaitingClient or
                    ScheduleAgreementStatus.AwaitingClientReschedule) &&
                proposedAt is not null
                    ? proposedAt.Value
                    : scheduledAt;
            var responseWindowOpen = now < responseDeadline;
            var beforeCutoff = now < cutoff;
            var creatorGrace = createdAt >= cutoff && isCreator && now < graceExpiry;
            var canManageOriginal = agreement is (ScheduleAgreementStatus.Accepted or
                ScheduleAgreementStatus.RescheduleRejected or ScheduleAgreementStatus.AwaitingFreelancer) &&
                (agreement != ScheduleAgreementStatus.AwaitingFreelancer || isCreator);
            var canCancelOriginal = canManageOriginal ||
                agreement == ScheduleAgreementStatus.AwaitingClientReschedule;
            var canRespond =
                agreement == ScheduleAgreementStatus.AwaitingFreelancer && !isCreator ||
                agreement is (ScheduleAgreementStatus.AwaitingClient or
                    ScheduleAgreementStatus.AwaitingClientReschedule) && isCreator;

            ScheduleMeetingResponse? meeting = null;
            if (currentSchedule is not null && currentSchedule.MeetingProvider != ScheduleMeetingProvider.None)
            {
                meeting = new ScheduleMeetingResponse(
                    (int)currentSchedule.MeetingProvider,
                    (int)currentSchedule.MeetingStatus,
                    createdBy,
                    currentSchedule.MeetingStatus == MeetingProvisioningStatus.Ready
                        ? currentSchedule.MeetingJoinUri
                        : null,
                    currentSchedule.MeetingFailureCode,
                    isCreator && currentSchedule.MeetingStatus == MeetingProvisioningStatus.Failed &&
                    isScheduled && hasNotStarted);
            }
            else if (schedule.Meeting is not null &&
                     schedule.Meeting.Provider != (int)ScheduleMeetingProvider.None)
            {
                var viewerCanRetry = isCreator &&
                    schedule.Meeting.Status == (int)MeetingProvisioningStatus.Failed &&
                    isScheduled && hasNotStarted;

                meeting = schedule.Meeting with { CanRetry = viewerCanRetry };
            }

            return schedule with
            {
                Status = (int)status,
                Title = currentSchedule?.Title ?? schedule.Title,
                Details = currentSchedule?.Details ?? schedule.Details,
                ScheduledAtUtc = scheduledAt,
                TimeZoneId = currentSchedule?.TimeZoneId ?? schedule.TimeZoneId,
                CreatedByUserId = createdBy,
                EditCount = editCount,
                RemainingEdits = remainingEdits,
                Version = currentSchedule?.Version ?? schedule.Version,
                CancellationReason = currentSchedule?.CancellationReason ?? schedule.CancellationReason,
                CutoffUtc = cutoff,
                GraceExpiresAtUtc = graceExpiry,
                AgreementStatus = (int)agreement,
                CounterProposalCreatedAtUtc = counterCreated,
                CounterProposalEditExpiresAtUtc = counterEditExpiry,
                ProposedScheduledAtUtc = proposedAt,
                ProposedTimeZoneId = proposedTimeZoneId,
                RescheduleRequestCount = rescheduleRequestCount,
                RemainingRescheduleRequests = remainingRescheduleRequests,
                CanEdit = isScheduled && hasNotStarted && isCreator && remainingEdits > 0 && canManageOriginal &&
                    (beforeCutoff || creatorGrace),
                CanCancel = isScheduled && hasNotStarted && canCancelOriginal &&
                    (beforeCutoff || creatorGrace),
                CanAccept = isScheduled && responseWindowOpen && canRespond &&
                    (agreement == ScheduleAgreementStatus.AwaitingFreelancer ||
                     proposedAt is not null && now < proposedAt.Value),
                CanReject = isScheduled && responseWindowOpen && canRespond,
                CanProposeTime = isScheduled && hasNotStarted && remainingRescheduleRequests > 0 &&
                    agreement is (ScheduleAgreementStatus.FreelancerRejectedAwaitingCounterproposal or
                        ScheduleAgreementStatus.Accepted or ScheduleAgreementStatus.RescheduleRejected) &&
                    !isCreator,
                CanEditCounterProposal = isScheduled && responseWindowOpen &&
                    agreement is (ScheduleAgreementStatus.AwaitingClient or
                        ScheduleAgreementStatus.AwaitingClientReschedule) && !isCreator &&
                    counterEditExpiry is not null && now < counterEditExpiry,
                Meeting = meeting
            };
        }
        catch
        {
            return null;
        }
    }
}
