using FluentValidation;

namespace PROManagementSystem.Application.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator
    : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Reset token is required.")
            .MaximumLength(256);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required.")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(100);
    }
}
