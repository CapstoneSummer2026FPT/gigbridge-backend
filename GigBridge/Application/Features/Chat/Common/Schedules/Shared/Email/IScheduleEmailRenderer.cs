namespace Application.Features.Chat.Common.Schedules;

public interface IScheduleEmailRenderer
{
    RenderedScheduleEmail Render(ScheduleNotificationType type, ScheduleEmailModel model);
}
