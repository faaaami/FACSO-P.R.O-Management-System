using FluentValidation;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientContact;

public sealed class UpdateClientContactCommandValidator
    : AbstractValidator<UpdateClientContactCommand>
{
    public UpdateClientContactCommandValidator()
    {
        RuleFor(x => x.ClientCompanyId)
            .NotEmpty();

        RuleFor(x => x.ContactId)
            .NotEmpty();

        RuleFor(x => x.Role)
    .Equal(UserRole.Client)
    .WithMessage(
        "A client contact must have the Client role.");
    }
}