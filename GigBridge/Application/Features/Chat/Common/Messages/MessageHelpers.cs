using System.Text.Json;
using Application.Features.Chat.Common.Schedules;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Chat.Common.Messages;

public static class MessageHelpers
{
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
            var isScheduled = schedule.Status == (int)ScheduleStatus.Scheduled;
            var hasNotStarted = now < schedule.ScheduledAtUtc;
            var beforeCutoff = now < schedule.CutoffUtc;
            var createdAt = currentSchedule?.CreatedAt ?? schedule.CreatedAt;
            var wasCreatedMoreThan24HoursBeforeStart = createdAt < schedule.CutoffUtc;
            var creatorGrace = !wasCreatedMoreThan24HoursBeforeStart &&
                viewerUserId == schedule.CreatedByUserId && now < schedule.GraceExpiresAtUtc;
            var agreement = currentSchedule?.AgreementStatus ?? (ScheduleAgreementStatus)schedule.AgreementStatus;
            var currentStatus = currentSchedule?.Status ?? (ScheduleStatus)schedule.Status;
            var currentStart = currentSchedule?.ScheduledAtUtc ?? schedule.ScheduledAtUtc;
            var isCreator = viewerUserId == schedule.CreatedByUserId;
            var counterCreated = currentSchedule?.CounterProposalCreatedAtUtc ?? schedule.CounterProposalCreatedAtUtc;
            var counterExpiry = counterCreated is null
                ? (DateTime?)null
                : new[] { counterCreated.Value.AddHours(24), currentStart }.Min();
            var active = currentStatus == ScheduleStatus.Scheduled && now < currentStart;
            var canAccept = active &&
                (agreement == ScheduleAgreementStatus.AwaitingFreelancer && !isCreator ||
                 agreement == ScheduleAgreementStatus.AwaitingClient && isCreator);

            // Meeting provisioning changes independently of the immutable chat
            // event metadata. Prefer the current schedule row when hydrating
            // message history so refreshes retain the final Meet URL/status.
            ScheduleMeetingResponse? meeting = null;
            if (currentSchedule is not null &&
                currentSchedule.MeetingProvider != ScheduleMeetingProvider.None)
            {
                var viewerCanRetry = viewerUserId == currentSchedule.CreatedByUserId &&
                    currentSchedule.MeetingStatus == MeetingProvisioningStatus.Failed &&
                    currentSchedule.Status == ScheduleStatus.Scheduled &&
                    now < currentSchedule.ScheduledAtUtc;

                meeting = new ScheduleMeetingResponse(
                    (int)currentSchedule.MeetingProvider,
                    (int)currentSchedule.MeetingStatus,
                    currentSchedule.CreatedByUserId,
                    currentSchedule.MeetingStatus == MeetingProvisioningStatus.Ready
                        ? currentSchedule.MeetingJoinUri
                        : null,
                    currentSchedule.MeetingFailureCode,
                    viewerCanRetry);
            }
            else if (schedule.Meeting is not null && schedule.Meeting.Provider != (int)ScheduleMeetingProvider.None)
            {
                var viewerCanRetry = viewerUserId == schedule.CreatedByUserId &&
                    schedule.Meeting.Status == (int)MeetingProvisioningStatus.Failed &&
                    isScheduled && hasNotStarted;

                meeting = schedule.Meeting with { CanRetry = viewerCanRetry };
            }

            return schedule with
            {
                CreatedAt = createdAt,
                AgreementStatus = (int)agreement,
                CounterProposalCreatedAtUtc = counterCreated,
                CounterProposalEditExpiresAtUtc = counterExpiry,
                CanAccept = canAccept,
                CanReject = canAccept,
                CanProposeTime = currentStatus == ScheduleStatus.Scheduled &&
                    agreement == ScheduleAgreementStatus.FreelancerRejectedAwaitingCounterproposal && !isCreator,
                CanEditCounterProposal = active && agreement == ScheduleAgreementStatus.AwaitingClient &&
                    !isCreator && counterExpiry is not null && now < counterExpiry,
                CanEdit = active && schedule.RemainingEdits > 0 &&
                    agreement is ScheduleAgreementStatus.Accepted or ScheduleAgreementStatus.AwaitingFreelancer &&
                    (agreement != ScheduleAgreementStatus.AwaitingFreelancer || isCreator) &&
                    (beforeCutoff || creatorGrace),
                CanCancel = active &&
                    agreement is ScheduleAgreementStatus.Accepted or ScheduleAgreementStatus.AwaitingFreelancer &&
                    (agreement != ScheduleAgreementStatus.AwaitingFreelancer || isCreator) &&
                    (beforeCutoff || creatorGrace),
                Meeting = meeting
            };
        }
        catch
        {
            return null;
        }
    }
}
