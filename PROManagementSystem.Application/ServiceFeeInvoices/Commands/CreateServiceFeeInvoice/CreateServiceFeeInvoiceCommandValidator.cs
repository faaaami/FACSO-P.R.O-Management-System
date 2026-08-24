using FluentValidation;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.CreateServiceFeeInvoice;

public sealed class CreateServiceFeeInvoiceCommandValidator
    : AbstractValidator<CreateServiceFeeInvoiceCommand>
{
    public CreateServiceFeeInvoiceCommandValidator()
    {
        RuleFor(x => x.ClientCompanyId)
            .NotEmpty()
            .WithMessage("Client company is required.");

        RuleFor(x => x.RenewalTaskId)
            .NotEmpty()
            .WithMessage("Renewal task is required.");

        RuleFor(x => x.InvoiceNumber)
            .NotEmpty()
            .WithMessage("Invoice number is required.")
            .MaximumLength(100)
            .WithMessage("Invoice number must not exceed 100 characters.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Amount must be greater than or equal to 0.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.");

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.InvoiceDate)
            .WithMessage("Due date cannot be earlier than invoice date.");
    }
}
