using Application.DTOs.Admin;
using Application.Features.Admin.Disputes.Dto;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminDisputeService
{
    Task<PagedResultDto<DisputeDto>> GetAllAsync(PagedQueryDto query, CancellationToken cancellationToken);
    Task<DisputeDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<DisputeDetailDto> ReviewAsync(Guid id, DisputeReviewRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
    Task<DisputeDetailDto> ResolveAsync(Guid id, DisputeResolutionRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
}

