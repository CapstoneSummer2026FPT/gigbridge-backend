namespace Application.Common.InternalServices.Chat.Models;
public sealed record JobAcceptanceEmailModel(
    string FreelancerName,
    string JobTitle,
    string FinalBudget,
    string ActionUrl);
