using Domain.Entities;

namespace Application.Common.Interfaces.IService;

public interface IUserEloService
{
    Task InitializeNewUserAsync(User user, CancellationToken cancellationToken);

    Task ApplyLoginActivityAsync(User user, CancellationToken cancellationToken);

    Task ApplyReviewScoreAsync(Guid reviewId, Guid revieweeId, int rating, CancellationToken cancellationToken);
}
