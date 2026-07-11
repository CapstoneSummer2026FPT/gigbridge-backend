using Application.Features.Premium.Freelancer.RankProtection.DTOs; using MediatR;
namespace Application.Features.Premium.Freelancer.RankProtection.ActivateRankProtection;
public sealed record ActivateRankProtectionCommand(Guid UserId, ActivateRankProtectionRequest Request) : IRequest<RankProtectionDto>;
