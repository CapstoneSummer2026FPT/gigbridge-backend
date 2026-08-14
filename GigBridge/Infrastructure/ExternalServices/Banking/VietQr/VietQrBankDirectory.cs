using Application.Features.Wallets.Common.Models;
using System.Text.Json;
using Application.Features.Wallets.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ExternalServices.Banking.VietQr;

public sealed class VietQrBankDirectory : ISupportedBankDirectory
{
    private const string CacheKey = "vietqr-supported-banks";
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VietQrBankDirectory> _logger;

    public VietQrBankDirectory(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<VietQrBankDirectory> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SupportedBank>> GetBanksAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<SupportedBank>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using var response = await _httpClient.GetAsync("v2/banks", cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var banks = document.RootElement.GetProperty("data")
                .EnumerateArray()
                .Select(item => new SupportedBank(
                    item.GetProperty("bin").GetString() ?? string.Empty,
                    item.GetProperty("code").GetString() ?? string.Empty,
                    item.GetProperty("shortName").GetString() ?? string.Empty,
                    item.GetProperty("name").GetString() ?? string.Empty,
                    item.TryGetProperty("logo", out var logo) ? logo.GetString() : null))
                .Where(bank => bank.Bin.Length == 6 && bank.Bin.All(char.IsDigit))
                .OrderBy(bank => bank.ShortName)
                .ToArray();

            _cache.Set(CacheKey, banks, TimeSpan.FromHours(24));
            return banks;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not refresh the VietQR bank directory.");
            return Array.Empty<SupportedBank>();
        }
    }
}
