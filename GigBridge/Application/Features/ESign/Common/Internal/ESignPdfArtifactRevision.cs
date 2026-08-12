using Domain.Entities;

namespace Application.Features.ESign.Common.Internal;

internal static class ESignPdfArtifactRevision
{
    public const string ContractFileName = "Gigbridge-Client-Freelancer-Contract.pdf";
    public const string ClientRendered = ":client-pdf-v2";
    public const string ContractTemplate = ":contract-template-pdf-v4";

    public static string ExpectedHash(EsignDocument document) =>
        $"{document.DocumentHash}{(document.ContractsId.HasValue ? ContractTemplate : ClientRendered)}";
}
