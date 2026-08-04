using FluentValidation;
namespace Application.Features.Premium.Freelancer.RankProtection.CancelRankProtection;
public sealed class CancelRankProtectionCommandValidator:AbstractValidator<CancelRankProtectionCommand>{public CancelRankProtectionCommandValidator(){RuleFor(x=>x.UserId).NotEmpty();}}
