using Application.Features.Premium.Freelancer.RankProtection.DTOs; using FluentValidation;
namespace Application.Features.Premium.Freelancer.RankProtection.ActivateRankProtection;
public sealed class ActivateRankProtectionCommandValidator:AbstractValidator<ActivateRankProtectionCommand>{public ActivateRankProtectionCommandValidator(){RuleFor(x=>x.UserId).NotEmpty();RuleFor(x=>x.Request.EndsAt).NotEmpty();RuleFor(x=>x.Request.Reason).MaximumLength(500);}}
