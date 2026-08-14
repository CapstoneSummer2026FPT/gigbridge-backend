using Application.Common.Interfaces;
using Application.Common.Interfaces.Templates;
using Application.Common.Interfaces.Time;
using Application.Features.Auth.Common.Interfaces;
using Application.Features.ESign.Common.Interfaces;
using Application.Features.Wallets.Common.Interfaces;
using Infrastructure.Adapters.Caching;
using Infrastructure.Adapters.Documents.ESign;
using Infrastructure.Adapters.Security.Auth;
using Infrastructure.Adapters.Security.Wallets;
using Infrastructure.Adapters.Templates;
using Infrastructure.Adapters.Time;
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
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IBankAccountProtector, BankAccountProtector>();
        services.AddSingleton<ITemplateReader, FileSystemTemplateReader>();
        services.AddTransient<IDateTimeService, DateTimeService>();
        services.AddHttpClient<IContractEsignDocumentGenerator, ContractEsignDocumentGenerator>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IWordToPdfConverter, WordToPdfConverter>();
        return services;
    }
}
