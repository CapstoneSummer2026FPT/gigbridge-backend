using Application.Features.Chat.Common.Models;

namespace Application.Features.Chat.Common.Interfaces;

public interface IGoogleMeetApiClient
{
    Task<CreateMeetSpaceResult> CreateSpaceAsync(
        string accessToken,
        CancellationToken cancellationToken);
}
