using Application.Features.Premium.Freelancer.RankProtection.DTOs;
using MediatR;
namespace Application.Features.Premium.Freelancer.RankProtection.GetRankProtection;
public sealed record GetRankProtectionQuery(Guid UserId) : IRequest<RankProtectionDto?>;
