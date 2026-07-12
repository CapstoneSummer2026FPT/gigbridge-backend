using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;

namespace Application.Features.Chat.Common.FinalOffers.Get.DTOs;

public sealed class NegotiationOfferDetailDto
{
    public Guid NegotiationOfferId { get; set; }
    public Guid ConversationId { get; set; }
    public decimal FinalPrice { get; set; }
    public string? ScopeSummary { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? ClientNote { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyCollection<NegotiationMilestoneDto> Milestones { get; set; } = [];
}
