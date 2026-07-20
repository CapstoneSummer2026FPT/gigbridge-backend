namespace Domain.Entities;

public sealed class ContractAmendmentSignature
{
    public Guid ContractAmendmentSignatureId { get; set; }
    public Guid ContractAmendmentId { get; set; }
    public Guid UserId { get; set; }
    public int SignerRole { get; set; }
    public string SignatureData { get; set; } = null!;
    public DateTime SignedAt { get; set; }

    public ContractAmendment Amendment { get; set; } = null!;
}
