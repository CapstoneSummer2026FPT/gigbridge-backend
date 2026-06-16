namespace Domain.Enums;

public enum ESignDocumentStatus
{
    Draft = 0,
    PendingSignatures = 1,
    PartiallySigned = 2,
    FullySigned = 3,
    Expired = 4,
    Voided = 5
}
