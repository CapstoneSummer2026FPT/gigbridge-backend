using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using FluentValidation;
using MediatR;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed record EndJobPostPromotionCommand(
    Guid UserId,
    Guid JobPostId) : IRequest<JobPostPromotionDto>;

public sealed class EndJobPostPromotionCommandValidator
    : AbstractValidator<EndJobPostPromotionCommand>
{
    public EndJobPostPromotionCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.JobPostId).NotEmpty();
    }
}
