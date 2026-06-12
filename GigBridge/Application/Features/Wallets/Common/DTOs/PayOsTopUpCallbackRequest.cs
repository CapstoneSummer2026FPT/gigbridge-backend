namespace Application.Features.Wallets.Common.DTOs;

public sealed record PayOsTopUpCallbackRequest(
    long? OrderCode,
    bool? Success,
    string? Code,
    string? Desc,
    string? GatewayTransactionCode,
    decimal? AmountVnd,
    string? Signature,
    PayOsTopUpCallbackData? Data);

public sealed record PayOsTopUpCallbackData(
    long? OrderCode,
    decimal? Amount,
    string? Description,
    string? AccountNumber,
    string? Reference,
    string? TransactionDateTime,
    string? Currency,
    string? PaymentLinkId,
    string? Code,
    string? Desc,
    string? CounterAccountBankId,
    string? CounterAccountBankName,
    string? CounterAccountName,
    string? CounterAccountNumber,
    string? VirtualAccountName,
    string? VirtualAccountNumber)
{
    public IReadOnlyDictionary<string, string?> ToSignatureData()
    {
        var data = new Dictionary<string, string?>();

        AddIfNotNull(data, "orderCode", OrderCode?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AddIfNotNull(data, "amount", Amount?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        AddIfNotNull(data, "description", Description);
        AddIfNotNull(data, "accountNumber", AccountNumber);
        AddIfNotNull(data, "reference", Reference);
        AddIfNotNull(data, "transactionDateTime", TransactionDateTime);
        AddIfNotNull(data, "currency", Currency);
        AddIfNotNull(data, "paymentLinkId", PaymentLinkId);
        AddIfNotNull(data, "code", Code);
        AddIfNotNull(data, "desc", Desc);
        AddIfNotNull(data, "counterAccountBankId", CounterAccountBankId);
        AddIfNotNull(data, "counterAccountBankName", CounterAccountBankName);
        AddIfNotNull(data, "counterAccountName", CounterAccountName);
        AddIfNotNull(data, "counterAccountNumber", CounterAccountNumber);
        AddIfNotNull(data, "virtualAccountName", VirtualAccountName);
        AddIfNotNull(data, "virtualAccountNumber", VirtualAccountNumber);

        return data;
    }

    private static void AddIfNotNull(
        IDictionary<string, string?> data,
        string key,
        string? value)
    {
        if (value is not null)
        {
            data[key] = value;
        }
    }
}
