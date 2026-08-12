namespace Application.Features.Contracts.Signing.Common.Sign.DTOs;

public sealed record SignedEmailModel(
    string RecipientName,
    string ContractTitle,
    string ContractCode);
