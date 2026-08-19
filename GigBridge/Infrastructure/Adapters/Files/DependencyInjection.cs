using Application.Common.Interfaces.Files;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Adapters.Files;

internal static class DependencyInjection
{
    internal static IServiceCollection AddFileUploadAdapter(this IServiceCollection services)
    {
        services.AddSingleton<IWorkspaceUploadFilePolicy, WorkspaceUploadFilePolicy>();
        return services;
    }
}
