namespace Application.Features.Portfolios.Common.DTOs;

public class PortfolioItemInputDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ProjectUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateOnly? ProjectDate { get; set; }
}
