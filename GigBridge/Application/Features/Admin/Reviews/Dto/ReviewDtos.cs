using Application.DTOs.Admin;

namespace Application.Features.Admin.Reviews.Dto;

public sealed class ReviewPageQueryDto : PagedQueryDto
{
    public bool? IsVisible { get; set; }
}

public sealed class ReviewDto
{
    public Guid ReviewId { get; init; }
    public Guid ContractId { get; init; }
    public Guid ReviewerId { get; init; }
    public Guid RevieweeId { get; init; }
    public string? ContractTitle { get; init; }
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public bool? IsVisible { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class ReviewVisibilityRequestDto
{
    public bool IsVisible { get; set; }
    public string? Reason { get; set; }
}
