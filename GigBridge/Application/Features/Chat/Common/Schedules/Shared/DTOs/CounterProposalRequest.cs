namespace Application.Features.Chat.Common.Schedules;

public record CounterProposalRequest(DateTimeOffset ScheduledAt, int ExpectedVersion,
    string TimeZoneId = "Asia/Ho_Chi_Minh");
