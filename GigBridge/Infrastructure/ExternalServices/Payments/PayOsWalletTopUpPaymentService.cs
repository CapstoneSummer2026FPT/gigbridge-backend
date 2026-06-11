using Application.Common.Interfaces.IService;
using PayOS;

namespace Infrastructure.ExternalServices.Payments;

public sealed class PayOsWalletTopUpPaymentService : IWalletTopUpPaymentService
{
    private const string GatewayProvider = "PayOS";
    private readonly PayOSClient _client;

    public PayOsWalletTopUpPaymentService(PayOSClient client)
    {
        _client = client;
    }

    public async Task<WalletTopUpPaymentResult> CreatePaymentAsync(
        WalletTopUpPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var resourceType = GetPayOsType("PayOS.Resources.V2.PaymentRequests.PaymentRequests");
            var requestType = GetPayOsType("PayOS.Models.V2.PaymentRequests.CreatePaymentLinkRequest");
            var itemType = GetPayOsType("PayOS.Models.V2.PaymentRequests.PaymentLinkItem");

            var resource = Activator.CreateInstance(resourceType, _client)
                ?? throw new InvalidOperationException("Cannot create PayOS payment resource.");
            var paymentRequest = Activator.CreateInstance(requestType)
                ?? throw new InvalidOperationException("Cannot create PayOS payment request.");

            SetProperty(paymentRequest, "OrderCode", request.OrderCode);
            SetProperty(paymentRequest, "Amount", Convert.ToInt32(request.AmountVnd));
            SetProperty(paymentRequest, "Description", ShortDescription(request.OrderCode));
            SetProperty(paymentRequest, "ReturnUrl", request.ReturnUrl ?? "https://gigbridge.local/wallet/top-up/success");
            SetProperty(paymentRequest, "CancelUrl", request.CancelUrl ?? "https://gigbridge.local/wallet/top-up/cancel");

            var item = Activator.CreateInstance(itemType)
                ?? throw new InvalidOperationException("Cannot create PayOS payment item.");
            SetProperty(item, "Name", "GigBridge tokens");
            SetProperty(item, "Quantity", 1);
            SetProperty(item, "Price", Convert.ToInt32(request.AmountVnd));

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
            return new WalletTopUpPaymentResult(
                GatewayProvider,
                request.OrderCode.ToString(),
                GetStringProperty(result, "PaymentLinkId") ?? GetStringProperty(result, "Id"),
                GetStringProperty(result, "CheckoutUrl"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("Unable to create PayOS wallet top-up request.", ex);
        }
    }

    public Task<WalletTopUpCallbackResult> VerifyCallbackAsync(
        WalletTopUpCallbackPayload payload,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new WalletTopUpCallbackResult(
            payload.OrderCode,
            payload.IsSucceeded,
            payload.GatewayTransactionCode,
            payload.AmountVnd,
            payload.FailureReason));
    }

    private static Type GetPayOsType(string typeName)
    {
        return Type.GetType($"{typeName}, PayOS")
            ?? throw new InvalidOperationException($"PayOS type {typeName} was not found.");
    }

    private static string ShortDescription(long orderCode)
    {
        return $"GB tokens {orderCode % 100000000}";
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
