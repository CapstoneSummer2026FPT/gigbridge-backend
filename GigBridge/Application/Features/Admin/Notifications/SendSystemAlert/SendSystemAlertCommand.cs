using Application.DTOs.Admin;
using MediatR;

namespace Application.Features.Admin.Notifications.SendSystemAlert;

public sealed record SendSystemAlertCommand(
    SystemAlertRequestDto Request,
    AdminActorDto Actor) : IRequest<SystemAlertResultDto>;
