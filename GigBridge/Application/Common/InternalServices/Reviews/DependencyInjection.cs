using Application.Common.InternalServices.Reviews.Interfaces;
using Application.Common.InternalServices.Reviews.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Reviews;
internal static class DependencyInjection
{
    internal static IServiceCollection AddReviewServices(this IServiceCollection services)
    {
        services.AddScoped<IReviewModerationService, ReviewModerationService>();
        return services;
    }
}
