namespace Application.Common.InternalServices.ESign.Models;
public sealed record SignedEmailModel(
    string RecipientName,
    string ContractTitle,
    string ContractCode);
