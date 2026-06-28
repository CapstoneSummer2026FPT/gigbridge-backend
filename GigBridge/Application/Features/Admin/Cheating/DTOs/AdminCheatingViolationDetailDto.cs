namespace Application.Features.Admin.Cheating.DTOs;

public class AdminCheatingViolationDetailDto : AdminCheatingViolationDto
{
    public IReadOnlyList<AdminCheatingEventDto> Events { get; init; } = Array.Empty<AdminCheatingEventDto>();
}
