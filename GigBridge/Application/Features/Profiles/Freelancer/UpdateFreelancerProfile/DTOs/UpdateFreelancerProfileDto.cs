using Application.Features.Portfolios.Common.DTOs;

namespace Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.DTOs;

public class UpdateFreelancerProfileDto
{
    public string Title { get; set; } = null!;
    public string Bio { get; set; } = null!;
    public int Availability { get; set; }
    public string Location { get; set; } = null!;
    public Guid MajorId { get; set; }
    public IReadOnlyCollection<Guid> CategoryIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyCollection<Guid>? SkillIds { get; set; }
    public IReadOnlyCollection<UpdatePortfolioItemDto>? PortfolioItems { get; set; }
}

public sealed class UpdatePortfolioItemDto : PortfolioItemInputDto
{
    public Guid? PortfolioItemId { get; set; }
}
