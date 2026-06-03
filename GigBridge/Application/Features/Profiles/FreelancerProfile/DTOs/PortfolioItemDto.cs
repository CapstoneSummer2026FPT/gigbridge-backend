using System;

namespace Application.Features.Profiles.FreelancerProfile.DTOs;

public class PortfolioItemDto
{
    public Guid PortfolioItemId { get; set; }
    public string? ProjectUrl { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
}
