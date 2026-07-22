namespace Application.Features.Chat.Common.FinalOffers.Shared.Email;

public interface IJobAcceptanceEmailRenderer
{
    RenderedJobAcceptanceEmail Render(JobAcceptanceEmailModel model);
}
