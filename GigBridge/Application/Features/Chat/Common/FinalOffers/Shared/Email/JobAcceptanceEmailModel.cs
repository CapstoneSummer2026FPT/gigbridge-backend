namespace Application.Features.Chat.Common.FinalOffers.Shared.Email;

public sealed record JobAcceptanceEmailModel(
    string FreelancerName,
    string JobTitle,
    string FinalBudget,
    string ActionUrl);
