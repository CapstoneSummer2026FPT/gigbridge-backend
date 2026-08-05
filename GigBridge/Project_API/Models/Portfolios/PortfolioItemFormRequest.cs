using Application.Features.Portfolios.Common.DTOs;

namespace Project_API.Models.Portfolios;

public class PortfolioItemFormRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ProjectUrl { get; set; }
    public DateOnly? ProjectDate { get; set; }
    public IFormFile? Image { get; set; }

    public PortfolioItemInputDto ToInputDto() => new()
    {
        Title = Title,
        Description = Description,
        ProjectUrl = ProjectUrl,
        ProjectDate = ProjectDate
    };
}

public sealed class UpdatePortfolioItemFormRequest : PortfolioItemFormRequest
{
    public bool RemoveImage { get; set; }
}
