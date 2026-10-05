using FluentValidation;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.UpdateRenewalTaskStatus;

public sealed class UpdateRenewalTaskStatusCommandValidator
    : AbstractValidator<UpdateRenewalTaskStatusCommand>
{
    public UpdateRenewalTaskStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Status is required.")
            .Must(x =>
            {
                if (string.IsNullOrWhiteSpace(x))
                {
                    return false;
                }

                return Enum.TryParse<RenewalTaskStatus>(
                        x,
                        ignoreCase: true,
                        out var parsed) &&
                    Enum.IsDefined(typeof(RenewalTaskStatus), parsed);
            })
            .WithMessage(
                "Status must be a valid renewal task status value.");

        RuleFor(x => x.Note)
            .MaximumLength(1000)
            .When(x => x.Note is not null)
            .WithMessage("Note must be at most 1000 characters.");

        RuleFor(x => x.RenewedExpiryDate)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage(
                "A renewed expiry date is required when the task status is set to Updated.")
            // DATE SEMANTICS. Persistence keeps only the date component
            // (ToUtcExpiryDate -> value.Date at UTC midnight), so this must
            // compare the normalized date against today's UTC date. Comparing
            // the raw timestamp let a value carrying today's date with a later
            // time of day pass, then persist as today's midnight, which is
            // already expired.
            .Must(x => x!.Value.Date > DateTime.UtcNow.Date)
            .WithMessage(
                "The renewed expiry date must be a date after today (UTC).")
            .When(IsCompletingTask);
    }

    private static bool IsCompletingTask(
        UpdateRenewalTaskStatusCommand command)
    {
        return Enum.TryParse<RenewalTaskStatus>(
                command.Status,
                ignoreCase: true,
                out var parsed) &&
            parsed == RenewalTaskStatus.Updated;
    }
}