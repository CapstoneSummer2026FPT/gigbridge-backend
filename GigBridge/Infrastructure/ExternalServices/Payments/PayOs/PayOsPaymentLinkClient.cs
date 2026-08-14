using PayOS;
using System.Collections;
using System.Globalization;

namespace Infrastructure.ExternalServices.Payments.PayOs;

public interface IPayOsPaymentLinkClient
{
    Task<PayOsPaymentLinkResult> CreateAsync(
        PayOsPaymentLinkRequest request,
        CancellationToken cancellationToken);

    Task<PayOsPaymentLinkStatusResult> GetAsync(
        long orderCode,
        CancellationToken cancellationToken);
}

public sealed record PayOsPaymentLinkRequest(
    long OrderCode,
    int Amount,
    string Description,
    string ReturnUrl,
    string CancelUrl);

public sealed record PayOsPaymentLinkResult(
    string? PaymentLinkId,
    string? CheckoutUrl);

public sealed record PayOsPaymentLinkStatusResult(
    string? PaymentLinkId,
    long? OrderCode,
    decimal? Amount,
    string? Status,
    string? Reference);

internal sealed class PayOsPaymentLinkClient : IPayOsPaymentLinkClient
{
    private readonly PayOSClient _client;

    public PayOsPaymentLinkClient(PayOSClient client)
    {
        _client = client;
    }

    public async Task<PayOsPaymentLinkResult> CreateAsync(
        PayOsPaymentLinkRequest request,
        CancellationToken cancellationToken)
    {
        var resourceType = GetPayOsType("PayOS.Resources.V2.PaymentRequests.PaymentRequests");
        var requestType = GetPayOsType("PayOS.Models.V2.PaymentRequests.CreatePaymentLinkRequest");
        var itemType = GetPayOsType("PayOS.Models.V2.PaymentRequests.PaymentLinkItem");

        var resource = Activator.CreateInstance(resourceType, _client)
            ?? throw new InvalidOperationException("Cannot create PayOS payment resource.");
        var paymentRequest = Activator.CreateInstance(requestType)
            ?? throw new InvalidOperationException("Cannot create PayOS payment request.");

        SetProperty(paymentRequest, "OrderCode", request.OrderCode);
        SetProperty(paymentRequest, "Amount", request.Amount);
        SetProperty(paymentRequest, "Description", request.Description);
        SetProperty(paymentRequest, "ReturnUrl", request.ReturnUrl);
        SetProperty(paymentRequest, "CancelUrl", request.CancelUrl);

        var item = Activator.CreateInstance(itemType)
            ?? throw new InvalidOperationException("Cannot create PayOS payment item.");
        SetProperty(item, "Name", "GigBridge tokens");
        SetProperty(item, "Quantity", 1);
        SetProperty(item, "Price", request.Amount);

        var items = Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))
            ?? throw new InvalidOperationException("Cannot create PayOS payment item list.");
        items.GetType().GetMethod("Add")!.Invoke(items, new[] { item });
        SetProperty(paymentRequest, "Items", items);

        var createMethod = resourceType.GetMethods()
            .First(method => method.Name == "CreateAsync" && method.GetParameters().Length == 2);

        var task = createMethod.Invoke(resource, new[] { paymentRequest, null }) as Task
            ?? throw new InvalidOperationException("PayOS CreateAsync did not return a task.");

        await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        return new PayOsPaymentLinkResult(
            GetStringProperty(result, "PaymentLinkId") ?? GetStringProperty(result, "Id"),
            GetStringProperty(result, "CheckoutUrl"));
    }

    public async Task<PayOsPaymentLinkStatusResult> GetAsync(
        long orderCode,
        CancellationToken cancellationToken)
    {
        var resourceType = GetPayOsType("PayOS.Resources.V2.PaymentRequests.PaymentRequests");
        var resource = Activator.CreateInstance(resourceType, _client)
            ?? throw new InvalidOperationException("Cannot create PayOS payment resource.");

        var getMethod = resourceType.GetMethods()
            .First(method =>
                method.Name == "GetAsync" &&
                method.GetParameters().Length == 2 &&
                method.GetParameters()[0].ParameterType == typeof(long));

        var task = getMethod.Invoke(resource, new object?[] { orderCode, null }) as Task
            ?? throw new InvalidOperationException("PayOS GetAsync did not return a task.");

        await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var result = task.GetType().GetProperty("Result")?.GetValue(task);

        return new PayOsPaymentLinkStatusResult(
            GetStringProperty(result, "PaymentLinkId") ?? GetStringProperty(result, "Id"),
            GetLongProperty(result, "OrderCode"),
            GetDecimalProperty(result, "Amount") ?? GetDecimalProperty(result, "AmountPaid"),
            GetStringProperty(result, "Status"),
            GetFirstTransactionReference(result));
    }

    private static Type GetPayOsType(string typeName)
    {
        return Type.GetType($"{typeName}, PayOS")
            ?? throw new InvalidOperationException($"PayOS type {typeName} was not found.");
    }

    private static string? GetStringProperty(object? instance, params string[] names)
    {
        if (instance is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            var value = instance.GetType().GetProperty(name)?.GetValue(instance);
            var text = value?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static long? GetLongProperty(object? instance, params string[] names)
    {
        if (instance is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            var value = instance.GetType().GetProperty(name)?.GetValue(instance);
            if (value is null)
            {
                continue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            if (long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static decimal? GetDecimalProperty(object? instance, params string[] names)
    {
        if (instance is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            var value = instance.GetType().GetProperty(name)?.GetValue(instance);
            if (value is null)
            {
                continue;
            }

            if (value is decimal decimalValue)
            {
                return decimalValue;
            }

            try
            {
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
            }
            catch (InvalidCastException)
            {
            }
        }

        return null;
    }

    private static string? GetFirstTransactionReference(object? paymentLink)
    {
        var transactions = paymentLink?.GetType().GetProperty("Transactions")?.GetValue(paymentLink) as IEnumerable;
        if (transactions is null)
        {
            return null;
        }

        foreach (var transaction in transactions)
        {
            var reference = GetStringProperty(
                transaction,
                "Reference",
                "TransactionId",
                "PaymentLinkId",
                "Id");

            if (!string.IsNullOrWhiteSpace(reference))
            {
                return reference;
            }
        }

        return null;
    }

    private static void SetProperty(object instance, string name, object value)
    {
        var property = instance.GetType().GetProperty(name);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (targetType.IsInstanceOfType(value))
        {
            property.SetValue(instance, value);
            return;
        }

        property.SetValue(instance, Convert.ChangeType(value, targetType));
    }
}
