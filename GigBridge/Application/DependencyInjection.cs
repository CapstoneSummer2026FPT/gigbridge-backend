using System.Reflection;
using Application.Common.Behaviours;
using Application.Common.Interfaces.IService;
using Application.Common.Mappings;
using Application.Common.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application;

public static class DependencyInjection
{

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg => {
            cfg.LicenseKey = configuration["MediatR:LicenseKey"];

            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
        });

        services.AddAutoMapper(cfg => {
            cfg.LicenseKey = configuration["AutoMapper:LicenseKey"];
        }, typeof(MappingProfile));

        services.AddScoped<IUserEloService, UserEloService>();

        services.AddSingleton<DeadlineWarningService>();
        services.AddSingleton<IDeadlineWarningService>(sp => sp.GetRequiredService<DeadlineWarningService>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DeadlineWarningService>());

        return services;
    }
}
