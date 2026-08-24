using FluentValidation;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientCompany;

public sealed class CreateClientCompanyValidator
    : AbstractValidator<CreateClientCompanyCommand>
{
    public CreateClientCompanyValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.TradeLicenseNumber)
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .MaximumLength(30);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(320);

        RuleFor(x => x.Address)
            .MaximumLength(500);

        RuleFor(x => x.Emirate)
            .MaximumLength(100);
    }
}