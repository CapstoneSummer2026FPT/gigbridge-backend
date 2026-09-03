using Infrastructure.ExternalServices.Ai;
using Infrastructure.ExternalServices.Banking.VietQr;
using Infrastructure.ExternalServices.Cache;
using Infrastructure.ExternalServices.Email.Resend;
using Infrastructure.ExternalServices.Google.Auth;
using Infrastructure.ExternalServices.Google.Meet;
using Infrastructure.ExternalServices.Media.Cloudinary;
using Infrastructure.ExternalServices.Monitoring.Sentry;
using Infrastructure.ExternalServices.Payments.PayOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.ExternalServices;

internal static class DependencyInjection
{
    internal static IServiceCollection AddExternalServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAiExternalService(configuration);
        services.AddRedisExternalService(configuration);
        services.AddResendExternalService(configuration);
        services.AddGoogleAuthExternalService();
        services.AddGoogleMeetExternalService(configuration);
        services.AddCloudinaryExternalService(configuration);
        services.AddSentryExternalService(configuration);
        services.AddPayOsExternalService(configuration);
        services.AddVietQrExternalService();
        return services;
    }
}
