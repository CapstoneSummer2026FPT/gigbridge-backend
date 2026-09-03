using Application.Features.Admin.SystemTracking.Common.Models;

namespace Application.Features.Admin.SystemTracking.Common.Interfaces;

public interface ISystemErrorSource
{
    Task<SystemErrorSourceResult> GetErrorsAsync(
        int requestedLimit,
        CancellationToken cancellationToken);
}
