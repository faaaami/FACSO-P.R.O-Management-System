using FluentValidation;

namespace PROManagementSystem.Application.GovFeeDisbursements.Commands.CreateGovFeeDisbursement;

public sealed class CreateGovFeeDisbursementCommandValidator
    : AbstractValidator<CreateGovFeeDisbursementCommand>
{
    public CreateGovFeeDisbursementCommandValidator()
    {
        RuleFor(x => x.ClientCompanyId)
            .NotEmpty()
            .WithMessage("Client company is required.");

        RuleFor(x => x.FeeDescription)
            .NotEmpty()
            .WithMessage("Fee description is required.")
            .MaximumLength(2000)
            .WithMessage("Fee description must not exceed 2000 characters.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Amount must be greater than or equal to 0.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.");
    }
}
