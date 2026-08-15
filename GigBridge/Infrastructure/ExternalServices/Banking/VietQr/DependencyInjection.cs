using Application.Common.InternalServices.Wallets.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.ExternalServices.Banking.VietQr;

internal static class DependencyInjection
{
    internal static IServiceCollection AddVietQrExternalService(this IServiceCollection services)
    {
        services.AddHttpClient<ISupportedBankDirectory, VietQrBankDirectory>(client =>
        {
            client.BaseAddress = new Uri("https://api.vietqr.io/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }
}
