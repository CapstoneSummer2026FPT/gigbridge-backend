using Application.Common.InternalServices.Chat.Models;

namespace Application.Common.InternalServices.Chat.Interfaces;
public interface IGoogleMeetApiClient
{
    Task<CreateMeetSpaceResult> CreateSpaceAsync(
        string accessToken,
        CancellationToken cancellationToken);
}
