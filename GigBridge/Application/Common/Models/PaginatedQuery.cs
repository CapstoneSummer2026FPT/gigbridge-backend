namespace Application.Common.Models;
public abstract class PaginatedQuery {
    public const int MaxPageSize = 100;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; } = "asc";
}