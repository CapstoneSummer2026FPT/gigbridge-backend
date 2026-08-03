using Application.Features.Admin.SystemTracking.Common.DTOs;

namespace Application.Features.Admin.SystemTracking.Common.Interfaces;

public interface ISystemTrackingReader
{
    SystemTrackingSnapshot Snapshot(string environment, int requestedLimit);
}
