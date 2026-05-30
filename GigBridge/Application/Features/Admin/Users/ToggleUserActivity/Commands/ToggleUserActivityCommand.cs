using MediatR;

namespace Application.Features.Admin.Users.ToggleUserActivity.Commands;

public record ToggleUserActivityCommand(string Email) : IRequest<bool>;
