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
        DateTime utcNow)
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
            var creatorGrace = viewerUserId == schedule.CreatedByUserId && now < schedule.GraceExpiresAtUtc;

            return schedule with
            {
                CanEdit = isScheduled && hasNotStarted && schedule.RemainingEdits > 0 &&
                    (beforeCutoff || creatorGrace),
                CanCancel = isScheduled && hasNotStarted && beforeCutoff
            };
        }
        catch
        {
            return null;
        }
    }
}
