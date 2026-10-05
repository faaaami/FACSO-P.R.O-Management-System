using FluentValidation;

namespace PROManagementSystem.Application.Tasks.Commands.SetRenewalTaskServiceFee;

public sealed class SetRenewalTaskServiceFeeCommandValidator
    : AbstractValidator<SetRenewalTaskServiceFeeCommand>
{
    public SetRenewalTaskServiceFeeCommandValidator()
    {
        // GreaterThan(0), not GreaterThanOrEqualTo(0). This is deliberately
        // stricter than CreateRetainerInvoiceCommandValidator, which accepts a
        // zero amount: the amount recorded here is the one
        // CreateServiceFeeInvoiceOnTaskCompletedHandler later copies onto the
        // Service Fee invoice, and UpdateRenewalTaskStatusCommandHandler refuses
        // to complete a task whose ServiceFeeAmount is null or <= 0. A zero would
        // therefore be a value this module could persist and the completion gate
        // would then reject, so it is refused at the door rather than at the
        // far end of the workflow.
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0.");

        // The money convention in this codebase is numeric(18,2) — see
        // ServiceFeeInvoiceConfiguration, PaymentOrderConfiguration and
        // RetainerInvoiceConfiguration. renewal_tasks.ServiceFeeAmount is a
        // plain nullable `numeric` column, so nothing at the storage layer
        // would round a third decimal place; the invoice created at completion
        // would. Rejecting more than two decimal places here means the amount an
        // Admin reads back is exactly the amount that gets billed, instead of a
        // figure that silently changes at completion.
        // Expressed as a decimal-scale test rather than FluentValidation's
        // ScalePrecision helper: that overload is declared for `decimal?` and so
        // does not bind to a non-nullable `decimal` property, and widening this
        // command's Amount to nullable purely to reach a helper would misdescribe
        // the contract — the request record's field is required, and the
        // GreaterThan(0) rule above already rejects a missing value as 0.
        RuleFor(x => x.Amount)
            .Must(HasAtMostTwoDecimalPlaces)
            .WithMessage(
                "Amount must not have more than 2 decimal places.");
    }

    /// <summary>
    /// True when the value carries no more than two decimal places.
    ///
    /// Counted on the text form rather than arithmetically, because the question
    /// is what the caller TYPED, not what the value equals. A value of 10.10m is
    /// numerically equal to 10.1m and the two are indistinguishable once parsed,
    /// so both correctly pass; the test rejects only genuinely finer precision
    /// such as 0.005m, which is the case that would silently round on its way to
    /// a numeric(18,2) invoice column.
    /// </summary>
    private static bool HasAtMostTwoDecimalPlaces(decimal amount)
    {
        // The raw string preserves trailing zeros, so "10.100" is rejected on the
        // third decimal even though its value is exactly 10.1. That is
        // intentional: the column is numeric(18,2) and a three-decimal entry is
        // a figure the Admin most likely did not intend to be truncated.
        var text =
            amount
                .ToString(System.Globalization.CultureInfo.InvariantCulture);

        var separatorIndex = text.IndexOf('.');

        if (separatorIndex < 0)
        {
            return true;
        }

        var decimalPlaces = text.Length - separatorIndex - 1;

        return decimalPlaces <= 2;
    }
}
