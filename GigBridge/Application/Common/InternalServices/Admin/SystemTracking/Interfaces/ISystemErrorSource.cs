using Application.Common.InternalServices.Admin.SystemTracking.Models;

namespace Application.Common.InternalServices.Admin.SystemTracking.Interfaces;

public interface ISystemErrorSource
{
    Task<SystemErrorSourceResult> GetErrorsAsync(int requestedLimit, CancellationToken cancellationToken);
}
