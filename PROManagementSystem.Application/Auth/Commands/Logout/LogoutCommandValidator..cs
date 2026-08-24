using FluentValidation;

namespace PROManagementSystem.Application.Auth.Commands.Logout;

public sealed class LogoutCommandValidator
    : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required.");
    }
}