using FluentValidation;

namespace PROManagementSystem.Application.Notifications.Commands.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadCommandValidator
    : AbstractValidator<MarkNotificationAsReadCommand>
{
    public MarkNotificationAsReadCommandValidator()
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty()
            .WithMessage("Notification ID is required.");
    }
}