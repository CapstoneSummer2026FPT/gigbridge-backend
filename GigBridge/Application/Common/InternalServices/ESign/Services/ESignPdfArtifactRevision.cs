using Domain.Entities;

namespace Application.Common.InternalServices.ESign.Services;
internal static class ESignPdfArtifactRevision
{
    public const string ContractFileName = "Gigbridge-Client-Freelancer-Contract.pdf";
    public const string ClientRendered = ":client-pdf-v2";
    public const string ContractTemplate = ":contract-template-pdf-v5";

    public static string ExpectedHash(EsignDocument document) =>
        $"{document.DocumentHash}{(document.ContractsId.HasValue ? ContractTemplate : ClientRendered)}";
}
