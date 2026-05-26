using Application.DTOs.Admin;
using Application.Features.Admin.Reports.Dto;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminReportService
{
    Task<PagedResultDto<ReportDto>> GetAllAsync(ReportPageQueryDto query, CancellationToken cancellationToken);
    Task<ReportDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ReportDto> ReviewAsync(Guid id, ReportReviewRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
    Task<ReportDto> ResolveAsync(Guid id, ReportResolutionRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
}

