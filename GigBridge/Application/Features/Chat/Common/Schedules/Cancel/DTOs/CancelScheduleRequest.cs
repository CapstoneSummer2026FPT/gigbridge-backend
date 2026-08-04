namespace Application.Features.Chat.Common.Schedules;

public record CancelScheduleRequest(string Reason, int ExpectedVersion);
