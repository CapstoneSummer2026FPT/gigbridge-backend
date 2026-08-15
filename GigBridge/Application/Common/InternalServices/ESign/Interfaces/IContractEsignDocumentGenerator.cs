using Application.Common.InternalServices.ESign.Models;
using Application.Features.Contracts.Common.DTOs;

namespace Application.Common.InternalServices.ESign.Interfaces;
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
