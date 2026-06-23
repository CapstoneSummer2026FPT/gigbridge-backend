namespace Application.Features.Chat.Common.Schedules;

public sealed record RenderedScheduleEmail(string Subject, string HtmlBody, string TextBody);
