namespace Application.Features.Admin.Cheating.DTOs;

public class ReviewCheatingViolationRequest
{
    public bool IsReviewed { get; set; }

    public string? AdminNote { get; set; }
}
