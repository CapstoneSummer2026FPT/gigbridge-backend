using Application.Common.Models.Ai;

namespace Application.Common.Interfaces.IService;

public interface IAiServiceClient
{
    Task<JobPostGenerationResponseDto> GenerateJobDescriptionAsync(JobPostGenerationRequestDto request, CancellationToken cancellationToken = default);
}
