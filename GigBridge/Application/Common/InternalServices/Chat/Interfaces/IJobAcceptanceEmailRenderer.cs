using Application.Common.InternalServices.Chat.Models;
namespace Application.Common.InternalServices.Chat.Interfaces;
public interface IJobAcceptanceEmailRenderer
{
    RenderedJobAcceptanceEmail Render(JobAcceptanceEmailModel model);
}
