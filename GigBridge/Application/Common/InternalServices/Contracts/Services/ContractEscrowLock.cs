namespace Application.Common.InternalServices.Contracts.Services;
internal static class ContractEscrowLock
{
    private const long Namespace = 0x455343524F574C4B;

    public static long ForContract(Guid contractId) =>
        BitConverter.ToInt64(contractId.ToByteArray(), 0) ^ Namespace;

    public static long ForUser(Guid userId) =>
        BitConverter.ToInt64(userId.ToByteArray(), 0) ^ 0x5553455256494F4C;
}
