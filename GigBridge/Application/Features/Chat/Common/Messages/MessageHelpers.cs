using System.Text.Json;
using Application.Features.Chat.Common.Schedules;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Chat.Common.Messages;

public static class MessageHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ScheduleEventResponse? ParseScheduleMetadata(Message message)
    {
        if (message.MessageType != (int)MessageType.Schedule || string.IsNullOrWhiteSpace(message.Metadata))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScheduleEventResponse>(message.Metadata, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
