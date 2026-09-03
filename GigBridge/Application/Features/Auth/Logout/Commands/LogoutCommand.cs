using MediatR;

namespace Application.Features.Auth.Logout.Commands;

public sealed record LogoutCommand(IReadOnlyCollection<string> RefreshTokens) : IRequest;
