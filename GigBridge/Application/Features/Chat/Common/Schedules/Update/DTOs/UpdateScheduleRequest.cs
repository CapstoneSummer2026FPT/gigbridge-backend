namespace Application.Features.Chat.Common.Schedules;

public record UpdateScheduleRequest(string Title, string? Details, DateTimeOffset ScheduledAt, int ExpectedVersion);
