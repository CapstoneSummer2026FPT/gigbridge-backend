using Application.Common.InternalServices.Wallets.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Adapters.Wallets;

internal static class DependencyInjection
{
    internal static IServiceCollection AddWalletAdapter(this IServiceCollection services)
    {
        services.AddScoped<IWalletLedgerService, WalletLedgerService>();
        return services;
    }
}
