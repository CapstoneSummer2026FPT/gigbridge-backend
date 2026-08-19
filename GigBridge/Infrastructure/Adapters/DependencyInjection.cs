using Application.Common.Interfaces;
using Application.Common.Interfaces.Documents;
using Application.Common.Interfaces.Templates;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.InternalServices.ESign.Interfaces;
using Application.Common.InternalServices.Wallets.Interfaces;
using Infrastructure.Adapters.Caching;
using Infrastructure.Adapters.Delivery;
using Infrastructure.Adapters.Documents.ESign;
using Infrastructure.Adapters.Documents.Receipts;
using Infrastructure.Adapters.Files;
using Infrastructure.Adapters.Security.Auth;
using Infrastructure.Adapters.Security.Wallets;
using Infrastructure.Adapters.Templates;
using Infrastructure.Adapters.Time;
using Infrastructure.Adapters.Wallets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Adapters;

internal static class DependencyInjection
{
    internal static IServiceCollection AddInfrastructureAdapters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCachingAdapter(configuration);
        services.AddDeliveryAdapter();
        services.AddFileUploadAdapter();
        services.AddWalletAdapter();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IBankAccountProtector, BankAccountProtector>();
        services.AddSingleton<ITemplateReader, FileSystemTemplateReader>();
        services.AddTransient<IDateTimeService, DateTimeService>();
        services.AddHttpClient<IContractEsignDocumentGenerator, ContractEsignDocumentGenerator>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IProjectReceiptDocumentGenerator, ProjectReceiptDocumentGenerator>();
        services.AddScoped<IWordToPdfConverter, WordToPdfConverter>();
        return services;
    }
}
