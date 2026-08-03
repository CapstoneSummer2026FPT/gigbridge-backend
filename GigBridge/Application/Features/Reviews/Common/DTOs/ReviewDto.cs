namespace Application.Features.Reviews.Common.DTOs;

public class ReviewDto
{
    public Guid ReviewId { get; set; }

    public Guid ContractId { get; set; }

    public Guid JobPostId { get; set; }

    public string ProjectTitle { get; set; } = string.Empty;

    public Guid ReviewerId { get; set; }

    public string? ReviewerName { get; set; }

    public Guid RevieweeId { get; set; }

    /// <summary>Overall rating 1.0–5.0, one decimal place.</summary>
    public decimal Rating { get; set; }

    public string? Comment { get; set; }

    public int? CommunicationRating { get; set; }

    public int? QualityRating { get; set; }

    public int? TimelinessRating { get; set; }

    public bool IsVisible { get; set; }

    public bool IsAnonymous => !IsVisible;

    public DateTime CreatedAt { get; set; }
}
