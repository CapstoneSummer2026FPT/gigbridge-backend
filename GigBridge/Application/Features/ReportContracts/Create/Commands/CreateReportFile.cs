namespace Application.Features.ReportContracts.Create.Commands;

public sealed record CreateReportFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);
