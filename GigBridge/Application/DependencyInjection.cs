using System.Reflection;
using Application.Common.Behaviours;
using Application.Common.InternalServices.Accounts;
using Application.Common.InternalServices.Auditing;
using Application.Common.InternalServices.Delivery;
using Application.Common.InternalServices.Receipts;
using Application.Common.Mappings;
using Application.Features.Admin.AuditLogs.Common;
using Application.Features.Admin.Analytics.Common;
using Application.Features.Auth.Common;
using Application.Features.Chat.Common;
using Application.Features.Contracts.Completion.Common;
using Application.Features.Contracts.Milestones.Freelancer.Submit.Common;
using Application.Features.Elo.Common;
using Application.Features.ESign.Common;
using Application.Features.JobInvitations.Common;
using Application.Features.JobPosts.Common;
using Application.Features.MarketplaceAnalytics.Common.Services;
using Application.Features.Notifications.Common;
using Application.Features.Premium.Common;
using Application.Features.Proposals.Common;
using Application.Features.Reviews.Common.Moderation;
using Application.Features.Wallets.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Application;

public static class DependencyInjection
{

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = configuration["MediatR:LicenseKey"];

            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
        });

        services.AddAutoMapper(cfg =>
        {
            cfg.LicenseKey = configuration["AutoMapper:LicenseKey"];
        }, typeof(MappingProfile));
        services.AddAccountServices();
        services.AddAuditingServices();
        services.AddDeliveryServices(configuration);
        services.AddAdminAuditLogServices();
        services.AddAdminAnalyticsServices(configuration);
        services.AddAuthServices();
        services.AddChatServices(configuration);
        services.AddEloServices();
        services.AddESignServices();
        services.AddJobInvitationServices();
        services.AddMilestoneSubmissionServices();
        services.AddScoped<IReviewModerationService, ReviewModerationService>();
        services.AddNotificationServices();
        services.AddPremiumServices(configuration);
        services.AddProposalServices();
        services.AddReceiptServices(configuration);
        services.AddScoped<IMarketplaceAnalyticsRecorder, MarketplaceAnalyticsRecorder>();
        services.AddWalletServices(configuration);
        services.AddJobPostServices(configuration);
        services.AddContractCompletionBackgroundJobs(configuration);

        return services;
    }

}
