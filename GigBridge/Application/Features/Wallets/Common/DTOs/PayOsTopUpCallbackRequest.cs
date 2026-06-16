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
        return new Dictionary<string, string?>
        {
            ["orderCode"] = OrderCode?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["amount"] = Amount?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["description"] = Description,
            ["accountNumber"] = AccountNumber,
            ["reference"] = Reference,
            ["transactionDateTime"] = TransactionDateTime,
            ["currency"] = Currency,
            ["paymentLinkId"] = PaymentLinkId,
            ["code"] = Code,
            ["desc"] = Desc,
            ["counterAccountBankId"] = CounterAccountBankId,
            ["counterAccountBankName"] = CounterAccountBankName,
            ["counterAccountName"] = CounterAccountName,
            ["counterAccountNumber"] = CounterAccountNumber,
            ["virtualAccountName"] = VirtualAccountName,
            ["virtualAccountNumber"] = VirtualAccountNumber
        };
    }
}
