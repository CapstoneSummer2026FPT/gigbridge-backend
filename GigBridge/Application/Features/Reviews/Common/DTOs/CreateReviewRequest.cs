namespace Application.Features.Reviews.Common.DTOs;

public class CreateReviewRequest
{
    public Guid ContractId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public int? CommunicationRating { get; set; }

    public int? QualityRating { get; set; }

    public int? TimelinessRating { get; set; }

    public bool IsAnonymous { get; set; }
}
