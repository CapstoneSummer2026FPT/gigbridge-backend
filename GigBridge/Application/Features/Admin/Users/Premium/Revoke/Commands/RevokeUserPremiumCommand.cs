using MediatR;

namespace Application.Features.Admin.Users.Premium.Revoke.Commands;

public sealed record RevokeUserPremiumCommand(Guid UserId) : IRequest<bool>;
