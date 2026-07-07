using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces.IService;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Payments;

public sealed class PayOsPayoutProvider : IPayoutProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly PayOsOptions _options;

    public PayOsPayoutProvider(HttpClient httpClient, IOptions<PayOsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string ProviderName => "PayOS";

    public async Task<PayoutProviderResult> CreatePayoutAsync(
        PayoutCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ConfigureBaseAddress())
        {
            return SyncRequired("PayOS payout endpoint is not configured.");
        }

        var payload = new
        {
            orderCode = request.ProviderOrderCode,
            amount = Convert.ToInt64(decimal.Round(request.AmountVnd, 0, MidpointRounding.AwayFromZero)),
            bankCode = request.BankCode,
            accountNumber = request.AccountNumber,
            accountName = request.AccountName,
            description = request.Description,
            idempotencyKey = request.IdempotencyKey
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            _options.PayoutCreatePath ?? "/v2/payouts")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        AddHeaders(message);

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PayoutProviderResult(
                    PayoutProviderOutcome.SyncRequired,
                    null,
                    null,
                    response.StatusCode.ToString(),
                    $"PayOS payout create failed with HTTP {(int)response.StatusCode}.",
                    raw);
            }

            return MapRawResponse(raw);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SyncRequired(ex.Message);
        }
    }

    public async Task<PayoutProviderResult> GetPayoutStatusAsync(
        PayoutStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ConfigureBaseAddress())
        {
            return SyncRequired("PayOS payout endpoint is not configured.");
        }

        var path = (_options.PayoutStatusPath ?? "/v2/payouts/{orderCode}")
            .Replace("{orderCode}", Uri.EscapeDataString(request.ProviderOrderCode), StringComparison.Ordinal)
            .Replace("{payoutId}", Uri.EscapeDataString(request.ProviderPayoutId ?? string.Empty), StringComparison.Ordinal);

        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        AddHeaders(message);

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PayoutProviderResult(
                    PayoutProviderOutcome.SyncRequired,
                    request.ProviderPayoutId,
                    null,
                    response.StatusCode.ToString(),
                    $"PayOS payout status failed with HTTP {(int)response.StatusCode}.",
                    raw);
            }

            return MapRawResponse(raw);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SyncRequired(ex.Message);
        }
    }

    public Task<PayoutWebhookVerificationResult> VerifyWebhookAsync(
        PayoutWebhookVerificationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(request.RawPayload) ? "{}" : request.RawPayload);
        var signatureData = ExtractSignatureData(document.RootElement);
        var isValid = PayOsSignatureVerifier.IsValid(
            signatureData,
            request.Signature,
            _options.ChecksumKey);

        var eventId = GetString(document.RootElement, "eventId", "id", "webhookId") ??
            GetString(GetDataElement(document.RootElement), "eventId", "id", "webhookId");
        var orderCode = GetString(document.RootElement, "orderCode", "referenceCode") ??
            GetString(GetDataElement(document.RootElement), "orderCode", "referenceCode");
        var payoutId = GetString(document.RootElement, "payoutId", "id", "paymentId") ??
            GetString(GetDataElement(document.RootElement), "payoutId", "id", "paymentId");
        var transactionCode = GetString(document.RootElement, "transactionCode", "transactionId", "reference") ??
            GetString(GetDataElement(document.RootElement), "transactionCode", "transactionId", "reference");
        var rawStatus = GetString(document.RootElement, "status", "code") ??
            GetString(GetDataElement(document.RootElement), "status", "code");
        var failureReason = GetString(document.RootElement, "desc", "message", "failureReason") ??
            GetString(GetDataElement(document.RootElement), "desc", "message", "failureReason");

        return Task.FromResult(new PayoutWebhookVerificationResult(
            isValid,
            eventId,
            orderCode,
            payoutId,
            MapStatus(rawStatus),
            transactionCode,
            rawStatus,
            failureReason,
            request.RawPayload));
    }

    private bool ConfigureBaseAddress()
    {
        if (_httpClient.BaseAddress is not null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_options.PayoutBaseUrl))
        {
            return false;
        }

        _httpClient.BaseAddress = new Uri(_options.PayoutBaseUrl.TrimEnd('/') + "/");
        return true;
    }

    private void AddHeaders(HttpRequestMessage message)
    {
        if (!string.IsNullOrWhiteSpace(_options.ClientId))
        {
            message.Headers.TryAddWithoutValidation("x-client-id", _options.ClientId);
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            message.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        }
    }

    private static PayoutProviderResult MapRawResponse(string raw)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        var root = document.RootElement;
        var data = GetDataElement(root);
        var status = GetString(root, "status", "code") ?? GetString(data, "status", "code");
        var payoutId = GetString(root, "payoutId", "id", "paymentId") ?? GetString(data, "payoutId", "id", "paymentId");
        var transactionCode = GetString(root, "transactionCode", "transactionId", "reference") ??
            GetString(data, "transactionCode", "transactionId", "reference");
        var failureReason = GetString(root, "desc", "message", "failureReason") ??
            GetString(data, "desc", "message", "failureReason");

        return new PayoutProviderResult(
            MapStatus(status),
            payoutId,
            transactionCode,
            status,
            failureReason,
            raw);
    }

    private static PayoutProviderResult SyncRequired(string reason)
    {
        return new PayoutProviderResult(
            PayoutProviderOutcome.SyncRequired,
            null,
            null,
            null,
            reason,
            null);
    }

    private static PayoutProviderOutcome MapStatus(string? status)
    {
        var normalized = status?
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return normalized switch
        {
            "00" or "SUCCESS" or "SUCCEEDED" or "COMPLETED" or "PAID" => PayoutProviderOutcome.Succeeded,
            "FAILED" or "FAIL" or "REJECTED" or "CANCELLED" or "CANCELED" or "EXPIRED" => PayoutProviderOutcome.Failed,
            "ACCEPTED" or "PROCESSING" => PayoutProviderOutcome.Accepted,
            "PENDING" or "WAITING" => PayoutProviderOutcome.Pending,
            _ => PayoutProviderOutcome.SyncRequired
        };
    }

    private static JsonElement GetDataElement(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object
            ? data
            : default;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            var value = property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.TryGetInt64(out var number)
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : property.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string?> ExtractSignatureData(JsonElement root)
    {
        var target = GetDataElement(root);
        if (target.ValueKind != JsonValueKind.Object)
        {
            target = root;
        }

        var data = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (target.ValueKind != JsonValueKind.Object)
        {
            return data;
        }

        foreach (var property in target.EnumerateObject())
        {
            if (string.Equals(property.Name, "signature", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            data[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText()
            };
        }

        return data;
    }
}
