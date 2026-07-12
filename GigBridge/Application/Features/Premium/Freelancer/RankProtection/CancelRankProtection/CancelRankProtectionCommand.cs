using Application.Features.Premium.Freelancer.RankProtection.DTOs; using MediatR;
namespace Application.Features.Premium.Freelancer.RankProtection.CancelRankProtection;
public sealed record CancelRankProtectionCommand(Guid UserId) : IRequest<RankProtectionDto>;
