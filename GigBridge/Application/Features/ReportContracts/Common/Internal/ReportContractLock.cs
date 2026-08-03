namespace Application.Features.ReportContracts.Common.Internal;

public static class ReportContractLock
{
    private const long ReportNamespace = 0x52435054524C4B31;
    public static long ForReport(Guid reportId) => BitConverter.ToInt64(reportId.ToByteArray(), 0) ^ ReportNamespace;
}
