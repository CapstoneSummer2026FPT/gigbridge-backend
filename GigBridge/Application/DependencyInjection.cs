using System.Reflection;
using Application.Common.Behaviours;
using Application.Common.InternalServices.Accounts;
using Application.Common.InternalServices.Admin.Analytics;
using Application.Common.InternalServices.Admin.AuditLogs;
using Application.Common.InternalServices.Auditing;
using Application.Common.InternalServices.Auth;
using Application.Common.InternalServices.Chat;
using Application.Common.InternalServices.Contracts.Completion;
using Application.Common.InternalServices.Contracts.Milestones;
using Application.Common.InternalServices.Delivery;
using Application.Common.InternalServices.Elo;
using Application.Common.InternalServices.ESign;
using Application.Common.InternalServices.JobInvitations;
using Application.Common.InternalServices.JobPosts;
using Application.Common.InternalServices.MarketplaceAnalytics;
using Application.Common.InternalServices.Notifications;
using Application.Common.InternalServices.Premium;
using Application.Common.InternalServices.Proposals;
using Application.Common.InternalServices.Receipts;
using Application.Common.InternalServices.Realtime;
using Application.Common.InternalServices.Reviews;
using Application.Common.InternalServices.Wallets;
using Application.Common.InternalServices.WorkSignals;
using Application.Common.Mappings;
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
        services.AddRealtimeRevisionServices();
        services.AddWorkSignalServices(configuration);
        services.AddDeliveryServices(configuration);
        services.AddAdminAuditLogServices();
        services.AddAdminAnalyticsServices(configuration);
        services.AddAuthServices(configuration);
        services.AddChatServices(configuration);
        services.AddEloServices();
        services.AddESignServices();
        services.AddJobInvitationServices();
        services.AddMilestoneSubmissionServices();
        services.AddReviewServices();
        services.AddNotificationServices();
        services.AddPremiumServices(configuration);
        services.AddProposalServices();
        services.AddReceiptServices(configuration);
        services.AddMarketplaceAnalyticsServices();
        services.AddWalletServices(configuration);
        services.AddJobPostServices(configuration);
        services.AddContractCompletionBackgroundJobs(configuration);

        return services;
    }

}
