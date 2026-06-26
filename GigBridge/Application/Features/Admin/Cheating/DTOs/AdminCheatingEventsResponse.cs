namespace Application.Features.Admin.Cheating.DTOs;

public class AdminCheatingEventsResponse
{
    public required IReadOnlyList<AdminCheatingEventDto> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}
