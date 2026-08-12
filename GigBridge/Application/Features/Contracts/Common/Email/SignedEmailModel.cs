namespace Application.Features.Contracts.Common.Email;

public sealed record SignedEmailModel(
    string RecipientName,
    string ContractTitle,
    string ContractCode);
