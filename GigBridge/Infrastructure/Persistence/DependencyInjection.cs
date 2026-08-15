using Application.Common.Interfaces;
using Application.Common.InternalServices.Wallets.Interfaces;
using Infrastructure.Persistence.Delivery;
using Infrastructure.Persistence.Wallets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

internal static class DependencyInjection
{
    internal static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        var pooledConnectionString = DatabasePoolOptions.Apply(connectionString, configuration);

        services.AddDbContext<GigbridgeDbContext>(options =>
            options.UseNpgsql(pooledConnectionString));
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<GigbridgeDbContext>());
        services.AddDeliveryPersistence();
        services.AddScoped<IWalletLedgerService, WalletLedgerService>();

        services.AddDataProtection()
            .SetApplicationName("GigBridge")
            .PersistKeysToDbContext<GigbridgeDbContext>();

        services.AddHealthChecks()
            .AddDbContextCheck<GigbridgeDbContext>("Database");

        return services;
    }
}
