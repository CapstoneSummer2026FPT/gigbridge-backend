using Application.Common.InternalServices.Chat.Models;
namespace Application.Common.InternalServices.Chat.Interfaces;
public interface IScheduleEmailRenderer
{
    RenderedScheduleEmail Render(ScheduleNotificationType type, ScheduleEmailModel model);
}
