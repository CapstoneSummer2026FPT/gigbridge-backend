using Application.Common.InternalServices.Admin.SystemTracking.Models;

namespace Application.Common.InternalServices.Admin.SystemTracking.Interfaces;
public interface ISystemTrackingReader
{
    SystemTrackingSnapshot Snapshot(string environment, int requestedLimit);
}
