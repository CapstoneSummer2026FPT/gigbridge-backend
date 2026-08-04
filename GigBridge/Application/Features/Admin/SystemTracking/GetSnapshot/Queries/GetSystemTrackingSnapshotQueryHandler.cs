using Application.Features.Admin.SystemTracking.Common.DTOs;
using Application.Features.Admin.SystemTracking.Common.Interfaces;
using MediatR;

namespace Application.Features.Admin.SystemTracking.GetSnapshot.Queries;

public sealed class GetSystemTrackingSnapshotQueryHandler(ISystemTrackingReader reader)
    : IRequestHandler<GetSystemTrackingSnapshotQuery, SystemTrackingSnapshot>
{
    public Task<SystemTrackingSnapshot> Handle(GetSystemTrackingSnapshotQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(reader.Snapshot(request.Environment, request.Limit));
}
