using FluentValidation;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CreatePaymentOrder;

public sealed class CreatePaymentOrderCommandValidator
    : AbstractValidator<CreatePaymentOrderCommand>
{
    public CreatePaymentOrderCommandValidator()
    {
        RuleFor(x => x.InvoiceType)
            .IsInEnum()
            .WithMessage("Invalid invoice type.");

        RuleFor(x => x.RetainerInvoiceId)
            .NotEmpty()
            .When(x =>
                x.InvoiceType ==
                PaymentInvoiceType.RetainerInvoice)
            .WithMessage(
                "Retainer invoice ID is required for retainer invoice type.");

        RuleFor(x => x.ServiceFeeInvoiceId)
            .NotEmpty()
            .When(x =>
                x.InvoiceType ==
                PaymentInvoiceType.ServiceFeeInvoice)
            .WithMessage(
                "Service fee invoice ID is required for service fee invoice type.");

        RuleFor(x => x.Notes)
            .MaximumLength(5000)
            .WithMessage("Notes must not exceed 5000 characters.");
    }
}
