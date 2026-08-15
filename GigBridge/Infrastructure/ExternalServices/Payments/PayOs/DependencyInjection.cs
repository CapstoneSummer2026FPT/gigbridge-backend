using System.Net;
using System.Net.Sockets;
using Application.Common.InternalServices.Wallets.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PayOS;

namespace Infrastructure.ExternalServices.Payments.PayOs;

internal static class DependencyInjection
{
    internal static IServiceCollection AddPayOsExternalService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<PayOsOptions>()
            .Bind(configuration.GetSection(PayOsOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ClientId))
                {
                    options.ClientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
                }

                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    options.ApiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY");
                }

                if (string.IsNullOrWhiteSpace(options.ChecksumKey))
                {
                    options.ChecksumKey = Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");
                }
            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ClientId) &&
                    !string.IsNullOrWhiteSpace(options.ApiKey) &&
                    !string.IsNullOrWhiteSpace(options.ChecksumKey),
                "PayOS configuration is missing. Set PAYOS_CLIENT_ID, PAYOS_API_KEY, and PAYOS_CHECKSUM_KEY.")
            .ValidateOnStart();

        var withdrawalsEnabled = configuration.GetValue<bool>("WalletWithdrawals:Enabled");
        services
            .AddOptions<PayOsPayoutOptions>()
            .Bind(configuration.GetSection(PayOsPayoutOptions.SectionName))
            .PostConfigure(options =>
            {
                options.ClientId = ReadFallback(options.ClientId, "PAYOS_PAYOUT_CLIENT_ID");
                options.ApiKey = ReadFallback(options.ApiKey, "PAYOS_PAYOUT_API_KEY");
                options.ChecksumKey = ReadFallback(options.ChecksumKey, "PAYOS_PAYOUT_CHECKSUM_KEY");
                options.ProxyUrl = ReadFallback(options.ProxyUrl, "PAYOS_PAYOUT_PROXY_URL");
            })
            .Validate(
                options => !withdrawalsEnabled ||
                    (!string.IsNullOrWhiteSpace(options.ClientId) &&
                        !string.IsNullOrWhiteSpace(options.ApiKey) &&
                        !string.IsNullOrWhiteSpace(options.ChecksumKey)),
                "PayOS payout configuration is missing. Set PAYOS_PAYOUT_CLIENT_ID, PAYOS_PAYOUT_API_KEY, and PAYOS_PAYOUT_CHECKSUM_KEY.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ProxyUrl) ||
                    Uri.TryCreate(options.ProxyUrl, UriKind.Absolute, out var proxyUri) &&
                    (proxyUri.Scheme == Uri.UriSchemeHttp || proxyUri.Scheme == Uri.UriSchemeHttps),
                "PayOS payout proxy URL must be an absolute HTTP or HTTPS URL.")
            .ValidateOnStart();

        services.AddKeyedSingleton("OrderClient", (serviceProvider, _) =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<PayOsOptions>>()
                .Value;
            return new PayOSClient(new PayOSOptions
            {
                ClientId = options.ClientId,
                ApiKey = options.ApiKey,
                ChecksumKey = options.ChecksumKey,
                LogLevel = LogLevel.Warning,
            });
        });

        services.AddKeyedSingleton("PayoutClient", (serviceProvider, _) =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<PayOsPayoutOptions>>()
                .Value;
            var clientOptions = new PayOSOptions
            {
                ClientId = options.ClientId,
                ApiKey = options.ApiKey,
                ChecksumKey = options.ChecksumKey,
                LogLevel = LogLevel.Warning,
                MaxRetries = 0,
                TimeoutMs = 20_000,
            };
            clientOptions.HttpClient = string.IsNullOrWhiteSpace(options.ProxyUrl)
                ? new HttpClient(CreatePayoutDirectHandler())
                : new HttpClient(CreatePayoutProxyHandler(options.ProxyUrl));
            return new PayOSClient(clientOptions);
        });

        services.AddScoped<IWalletTopUpPaymentService, PayOsWalletTopUpPaymentService>();
        services.AddScoped<IPayOsPaymentLinkClient>(provider =>
            new PayOsPaymentLinkClient(provider.GetRequiredKeyedService<PayOSClient>("OrderClient")));
        services.AddScoped<IPayoutProvider>(provider =>
            new PayOsPayoutProvider(
                provider.GetRequiredKeyedService<PayOSClient>("PayoutClient"),
                provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));
        return services;
    }

    private static string? ReadFallback(string? configuredValue, string environmentVariable) =>
        string.IsNullOrWhiteSpace(configuredValue)
            ? Environment.GetEnvironmentVariable(environmentVariable)
            : configuredValue;

    internal static SocketsHttpHandler CreatePayoutDirectHandler() => new()
    {
        UseProxy = false,
        ConnectCallback = async (context, cancellationToken) =>
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    internal static HttpClientHandler CreatePayoutProxyHandler(string proxyUrl)
    {
        var proxyUri = new Uri(proxyUrl);
        var address = new UriBuilder(proxyUri) { UserName = string.Empty, Password = string.Empty }.Uri;
        var proxy = new WebProxy(address);
        if (!string.IsNullOrWhiteSpace(proxyUri.UserInfo))
        {
            var credentials = proxyUri.UserInfo.Split(':', 2);
            proxy.Credentials = new NetworkCredential(
                Uri.UnescapeDataString(credentials[0]),
                credentials.Length == 2 ? Uri.UnescapeDataString(credentials[1]) : string.Empty);
        }

        return new HttpClientHandler { Proxy = proxy, UseProxy = true };
    }
}
