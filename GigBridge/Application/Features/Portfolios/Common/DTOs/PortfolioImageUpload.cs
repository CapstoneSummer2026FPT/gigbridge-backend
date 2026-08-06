namespace Application.Features.Portfolios.Common.DTOs;

public sealed record PortfolioImageUpload(
    Stream Content,
    string FileName,
    string ContentType,
    long FileSize);
