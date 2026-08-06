using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;

namespace Test_Gigbridge_Backend.TestSupport;

internal sealed class FakeContractEsignDocumentGenerator : IContractEsignDocumentGenerator
{
    public List<GenerateCall> GenerateCalls { get; } = [];

    public string RenderPreview(ContractDocumentSnapshot snapshot) =>
        $"<article><p>{System.Net.WebUtility.HtmlEncode(snapshot.ProjectTitle)}</p></article>";

    public Task<GeneratedContractDocument> GenerateAsync(
        ContractDocumentSnapshot snapshot,
        ContractSignatureSnapshot? clientSignature,
        ContractSignatureSnapshot? freelancerSignature,
        string documentHash,
        CancellationToken cancellationToken)
    {
        GenerateCalls.Add(new GenerateCall(snapshot, clientSignature, freelancerSignature, documentHash));
        return Task.FromResult(new GeneratedContractDocument(
            [0x50, 0x4b, 0x03, 0x04],
            $"GigBridge-{snapshot.ContractCode}.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
    }

    public sealed record GenerateCall(
        ContractDocumentSnapshot Snapshot,
        ContractSignatureSnapshot? ClientSignature,
        ContractSignatureSnapshot? FreelancerSignature,
        string DocumentHash);
}
