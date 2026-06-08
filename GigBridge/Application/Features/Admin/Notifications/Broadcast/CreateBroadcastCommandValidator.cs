using Domain.Enums;
using FluentValidation;

namespace Application.Features.Admin.Notifications.Broadcast;

public class CreateBroadcastCommandValidator : AbstractValidator<CreateBroadcastCommand>
{
    public CreateBroadcastCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(300).WithMessage("Title must be at most 300 characters.");

        RuleFor(x => x.Content)
            .MaximumLength(2000).WithMessage("Content must be at most 2000 characters.");

        RuleFor(x => x.Type)
            .Must(type => Enum.IsDefined(typeof(NotificationType), type))
            .WithMessage("Invalid notification type.");

        RuleFor(x => x.Target)
            .IsInEnum().WithMessage("Invalid notification target.");

        RuleFor(x => x.TargetUserId)
            .NotNull().WithMessage("TargetUserId is required when target is User.")
            .When(x => x.Target == NotificationTarget.User);

        RuleFor(x => x.ReferenceType)
            .MaximumLength(50).WithMessage("ReferenceType must be at most 50 characters.");
    }
}
