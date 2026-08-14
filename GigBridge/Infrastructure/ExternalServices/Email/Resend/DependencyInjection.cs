using Application.Common.Interfaces.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Resend;

namespace Infrastructure.ExternalServices.Email.Resend;

internal static class DependencyInjection
{
    internal static IServiceCollection AddResendExternalService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var resendApiToken = configuration["Resend:ApiToken"]
            ?? Environment.GetEnvironmentVariable("RESEND_API_TOKEN");
        if (string.IsNullOrWhiteSpace(resendApiToken))
        {
            throw new InvalidOperationException(
                "Resend configuration is missing. Set Resend:ApiToken in appsettings or environment variable RESEND_API_TOKEN.");
        }

        services.AddResend(options => options.ApiToken = resendApiToken);
        services.AddScoped<IEmailService, EmailService>();
        return services;
    }
}
