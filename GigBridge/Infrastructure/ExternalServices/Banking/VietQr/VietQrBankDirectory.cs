using Application.Common.Exceptions;
using Application.Common.Interfaces.Caching;
using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Common.InternalServices.Wallets.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.ExternalServices.Banking.VietQr;

public sealed class VietQrBankDirectory : ISupportedBankDirectory
{
    private const string FreshCacheKey = "wallet:supported-banks:vietqr:fresh:v1";
    private const string StaleCacheKey = "wallet:supported-banks:vietqr:stale:v1";
    private static readonly TimeSpan FreshCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan StaleCacheDuration = TimeSpan.FromDays(7);
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly ICacheService _cache;
    private readonly ILogger<VietQrBankDirectory> _logger;

    public VietQrBankDirectory(
        HttpClient httpClient,
        ICacheService cache,
        ILogger<VietQrBankDirectory> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SupportedBank>> GetBanksAsync(CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync<SupportedBank[]>(FreshCacheKey, cancellationToken);
        if (HasBanks(cached))
        {
            return cached!;
        }

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            cached = await _cache.GetAsync<SupportedBank[]>(FreshCacheKey, cancellationToken);
            if (HasBanks(cached))
            {
                return cached!;
            }

            try
            {
                var banks = await RefreshAsync(cancellationToken);
                await _cache.SetAsync(FreshCacheKey, banks, FreshCacheDuration, cancellationToken);
                await _cache.SetAsync(StaleCacheKey, banks, StaleCacheDuration, cancellationToken);
                _logger.LogInformation("Refreshed {BankCount} supported banks from VietQR.", banks.Length);
                return banks;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var stale = await _cache.GetAsync<SupportedBank[]>(StaleCacheKey, cancellationToken);
                if (HasBanks(stale))
                {
                    _logger.LogWarning(
                        exception,
                        "VietQR refresh failed; using the stale supported-bank directory with {BankCount} banks.",
                        stale!.Length);
                    return stale;
                }

                _logger.LogWarning(exception, "VietQR refresh failed and no supported-bank cache is available.");
                throw new ExternalServiceException(
                    "The supported-bank directory is temporarily unavailable. Please try again later.",
                    exception);
            }
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private async Task<SupportedBank[]> RefreshAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            "v2/banks",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<VietQrBankListResponse>(
            stream,
            cancellationToken: cancellationToken);
        if (payload is null ||
            !string.Equals(payload.Code, "00", StringComparison.Ordinal) ||
            payload.Data is null)
        {
            throw new InvalidDataException("VietQR returned an unsuccessful bank-directory response.");
        }

        var banks = payload.Data
            .Select(MapBank)
            .Where(bank => bank is not null)
            .Select(bank => bank!)
            .GroupBy(bank => bank.Bin, StringComparer.Ordinal)
            .Select(group => group.OrderBy(bank => bank.Code, StringComparer.OrdinalIgnoreCase).First())
            .OrderBy(bank => bank.ShortName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(bank => bank.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (banks.Length == 0)
        {
            throw new InvalidDataException("VietQR returned no valid supported banks.");
        }

        return banks;
    }

    private static SupportedBank? MapBank(VietQrBankItem item)
    {
        var bin = item.Bin?.Trim() ?? string.Empty;
        var code = item.Code?.Trim() ?? string.Empty;
        var name = item.Name?.Trim() ?? string.Empty;
        var shortName = item.ShortName?.Trim() ?? string.Empty;

        if (bin.Length != 6 || bin.Any(character => !char.IsDigit(character)) ||
            string.IsNullOrWhiteSpace(code) || code.Length > 30 ||
            string.IsNullOrWhiteSpace(name) || name.Length > 120 ||
            shortName.Length > 60)
        {
            return null;
        }

        return new SupportedBank(
            bin,
            code.ToUpperInvariant(),
            string.IsNullOrWhiteSpace(shortName) ? code.ToUpperInvariant() : shortName,
            name,
            SanitizeLogo(item.Logo));
    }

    private static string? SanitizeLogo(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("api.vietqr.io", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    private static bool HasBanks(SupportedBank[]? banks) => banks is { Length: > 0 };

    private sealed record VietQrBankListResponse(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("data")] VietQrBankItem[]? Data);

    private sealed record VietQrBankItem(
        [property: JsonPropertyName("bin")] string? Bin,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("shortName")] string? ShortName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("logo")] string? Logo);
}
