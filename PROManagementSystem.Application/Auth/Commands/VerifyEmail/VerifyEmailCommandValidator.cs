using FluentValidation;

namespace PROManagementSystem.Application.Auth.Commands.VerifyEmail;

public sealed class VerifyEmailCommandValidator
    : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Verification token is required.");
    }
}