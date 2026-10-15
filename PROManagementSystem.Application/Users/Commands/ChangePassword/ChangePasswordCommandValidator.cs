using FluentValidation;

namespace PROManagementSystem.Application.Users.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator
    : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required.")
            .MinimumLength(8)
            .WithMessage("New password must be at least 8 characters.")
            .MaximumLength(100)
            .WithMessage("New password must be 100 characters or fewer.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Confirm password does not match new password.");

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("New password must be different from current password.");
    }
}
