using PayOS;

namespace Infrastructure.ExternalServices.Payments;

public interface IPayOsPaymentLinkClient
{
    Task<PayOsPaymentLinkResult> CreateAsync(
        PayOsPaymentLinkRequest request,
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

    private static Type GetPayOsType(string typeName)
    {
        return Type.GetType($"{typeName}, PayOS")
            ?? throw new InvalidOperationException($"PayOS type {typeName} was not found.");
    }

    private static string? GetStringProperty(object? instance, string name)
    {
        if (instance is null)
        {
            return null;
        }

        return instance.GetType().GetProperty(name)?.GetValue(instance)?.ToString();
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
