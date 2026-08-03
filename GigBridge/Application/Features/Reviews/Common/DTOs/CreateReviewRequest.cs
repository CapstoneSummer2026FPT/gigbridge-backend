namespace Application.Features.Reviews.Common.DTOs;

public class CreateReviewRequest
{
    public Guid ContractId { get; set; }

    /// <summary>Overall rating 1.0–5.0, one decimal place. Recomputed from the criteria sub-ratings by the handler.</summary>
    public decimal Rating { get; set; }

    public string? Comment { get; set; }

    public int? CommunicationRating { get; set; }

    public int? QualityRating { get; set; }

    public int? TimelinessRating { get; set; }

    public bool IsAnonymous { get; set; }
}
