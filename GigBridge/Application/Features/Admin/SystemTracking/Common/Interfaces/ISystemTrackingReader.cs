using Application.Features.Admin.SystemTracking.Common.Models;

namespace Application.Features.Admin.SystemTracking.Common.Interfaces;

public interface ISystemTrackingReader
{
    SystemTrackingSnapshot Snapshot(string environment, int requestedLimit);
}
