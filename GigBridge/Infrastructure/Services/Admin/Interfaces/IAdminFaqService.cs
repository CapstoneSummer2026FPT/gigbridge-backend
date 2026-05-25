using Application.DTOs.Admin;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminFaqService
{
    Task<PagedResultDto<FaqDto>> GetAllAsync(FaqPageQueryDto query, CancellationToken cancellationToken);
    Task<FaqDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<FaqDto> CreateAsync(SaveFaqRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
    Task<FaqDto> UpdateAsync(Guid id, SaveFaqRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, AdminActorDto actor, CancellationToken cancellationToken);
}
