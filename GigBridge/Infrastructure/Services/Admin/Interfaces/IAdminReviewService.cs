using Application.DTOs.Admin;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminReviewService
{
    Task<PagedResultDto<ReviewDto>> GetAllAsync(ReviewPageQueryDto query, CancellationToken cancellationToken);
    Task<ReviewDto> SetVisibilityAsync(Guid id, ReviewVisibilityRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
}
