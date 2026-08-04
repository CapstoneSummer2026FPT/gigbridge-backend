using Application.Features.Contracts.Common.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IContractEsignDocumentGenerator
{
    string RenderPreview(ContractDocumentSnapshot snapshot);

    Task<GeneratedContractDocument> GenerateFinalAsync(
        ContractDocumentSnapshot snapshot,
        ContractSignatureSnapshot clientSignature,
        ContractSignatureSnapshot freelancerSignature,
        string documentHash,
        CancellationToken cancellationToken);
}
