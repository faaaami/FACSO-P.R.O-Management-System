using FluentValidation;

namespace PROManagementSystem.Application.Users.Commands.UpdateMyProfile;

public sealed class UpdateMyProfileCommandValidator
    : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .WithMessage("Full name is required.")
            .MaximumLength(150)
            .WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .WithMessage("Phone number cannot exceed 20 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}