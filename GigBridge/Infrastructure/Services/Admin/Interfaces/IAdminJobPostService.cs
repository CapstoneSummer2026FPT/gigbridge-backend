using Application.DTOs.Admin;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminJobPostService
{
    Task<PagedResultDto<JobPostDto>> GetAllAsync(PagedQueryDto query, CancellationToken cancellationToken);
    Task<JobPostDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<JobPostDetailDto> CancelAsync(Guid id, JobStatusRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
}
