namespace Application.Common.InternalServices.ESign.Models;

public sealed record ESignDocumentRevisionDeliveryPayload(
    Guid DocumentId,
    Guid? ContractId,
    int Revision,
    string ChangeKind);
