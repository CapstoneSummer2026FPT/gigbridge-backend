using System.IO;

namespace Application.Features.Contracts.ProductHandoffs.Common.DTOs;

public sealed record SubmitContractProductHandoffFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);
