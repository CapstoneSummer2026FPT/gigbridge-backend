using Application.DTOs.Admin;
using Application.Features.Admin.Notifications.Dto;
using MediatR;

namespace Application.Features.Admin.Notifications.Command;

public sealed record SendSystemAlertCommand(
    SystemAlertRequestDto Request,
    AdminActorDto Actor) : IRequest<SystemAlertResultDto>;

