using Application.Common.InternalServices.Realtime.Models;
using MediatR;

namespace Application.Features.Notifications.Public.GetStatus.Queries;

public sealed record GetNotificationStatusQuery(Guid UserId) : IRequest<RealtimeStatusResponse>;
