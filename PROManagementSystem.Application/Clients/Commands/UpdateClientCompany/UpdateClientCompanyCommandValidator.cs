using FluentValidation;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientCompany;

public sealed class UpdateClientCompanyCommandValidator
    : AbstractValidator<UpdateClientCompanyCommand>
{
    public UpdateClientCompanyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.CompanyName)
            .MaximumLength(200)
            .When(x => x.CompanyName is not null);

        RuleFor(x => x.TradeLicenseNumber)
            .MaximumLength(100)
            .When(x => x.TradeLicenseNumber is not null);

        RuleFor(x => x.Phone)
            .MaximumLength(30)
            .When(x => x.Phone is not null);

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(320)
            .When(x => x.Email is not null);

        RuleFor(x => x.Address)
            .MaximumLength(500)
            .When(x => x.Address is not null);

        RuleFor(x => x.Emirate)
            .MaximumLength(100)
            .When(x => x.Emirate is not null);
    }
}