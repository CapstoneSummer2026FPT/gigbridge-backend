namespace Application.Features.Chat.Common.Schedules;

public record CreateScheduleRequest(Guid ConversationId, string Title, string? Details, DateTimeOffset ScheduledAt,
    string TimeZoneId = "Asia/Ho_Chi_Minh", bool AddGoogleMeet = false,
    bool SendEmailNotification = true);
