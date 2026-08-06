using Application.Features.Contracts.Common.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IContractEsignDocumentGenerator
{
    string RenderPreview(ContractDocumentSnapshot snapshot);

    Task<GeneratedContractDocument> GenerateAsync(
        ContractDocumentSnapshot snapshot,
        ContractSignatureSnapshot? clientSignature,
        ContractSignatureSnapshot? freelancerSignature,
        string documentHash,
        CancellationToken cancellationToken);
}
