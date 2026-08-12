namespace Application.Common.InternalServices.Accounts.Services;

public static class AccountEnforcementLock
{
    private const long Namespace = 0x5553455256494F4C;
    public static long ForUser(Guid userId) => BitConverter.ToInt64(userId.ToByteArray(), 0) ^ Namespace;
    public static long ForReport(Guid reportId) => BitConverter.ToInt64(reportId.ToByteArray(), 0) ^ 0x5245504F52544C4B;
}
