using MediatR;

namespace Application.Features.Admin.Users.Premium.Grant.Commands;

public sealed record GrantUserPremiumCommand(Guid UserId) : IRequest<bool>;
