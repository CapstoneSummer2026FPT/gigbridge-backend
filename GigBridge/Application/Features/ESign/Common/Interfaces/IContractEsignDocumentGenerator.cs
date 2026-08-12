using Application.Features.Contracts.Common.DTOs;

namespace Application.Features.ESign.Common.Interfaces;

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
