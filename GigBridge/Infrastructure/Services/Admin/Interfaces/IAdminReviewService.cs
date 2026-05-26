using Application.DTOs.Admin;
using Application.Features.Admin.Reviews.Dto;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminReviewService
{
    Task<PagedResultDto<ReviewDto>> GetAllAsync(ReviewPageQueryDto query, CancellationToken cancellationToken);
    Task<ReviewDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ReviewDto> SetVisibilityAsync(Guid id, ReviewVisibilityRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
}

