using Application.Features.Admin.SystemTracking.Common.Models;
using MediatR;

namespace Application.Features.Admin.SystemTracking.GetSnapshot.Queries;

public sealed record GetSystemTrackingSnapshotQuery(string Environment, int Limit = 100)
    : IRequest<SystemTrackingSnapshot>;
