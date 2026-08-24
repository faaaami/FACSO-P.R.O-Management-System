using FluentValidation;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientEntity;

public sealed class UpdateClientEntityCommandValidator
    : AbstractValidator<UpdateClientEntityCommand>
{
    public UpdateClientEntityCommandValidator()
    {
        RuleFor(x => x.ClientCompanyId)
            .NotEmpty();

        RuleFor(x => x.EntityId)
            .NotEmpty();

        RuleFor(x => x.EntityName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.TradeLicenseNumber)
            .MaximumLength(100)
            .When(x => x.TradeLicenseNumber is not null);

        RuleFor(x => x.Emirate)
            .MaximumLength(100)
            .When(x => x.Emirate is not null);
    }
}