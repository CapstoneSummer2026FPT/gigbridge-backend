namespace Application.Common.InternalServices.Contracts.Models;

public sealed record ContractPlanChangeEmailModel(
    string ClientName,
    string FreelancerName,
    string ContractTitle,
    string Reason,
    string ActionUrl);

public sealed record RenderedContractPlanChangeEmail(
    string Subject,
    string HtmlBody,
    string TextBody);
