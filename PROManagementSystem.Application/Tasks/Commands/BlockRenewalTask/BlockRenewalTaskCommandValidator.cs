using FluentValidation;

namespace PROManagementSystem.Application.Tasks.Commands.BlockRenewalTask;

public sealed class BlockRenewalTaskCommandValidator
    : AbstractValidator<BlockRenewalTaskCommand>
{
    /// <summary>
    /// Same bound, and for the same reason, as
    /// <c>UnblockRenewalTaskCommandHandler.MaxUnblockReasonLength</c>.
    ///
    /// A block reason is written to three columns with two different limits:
    /// renewal_tasks.blocked_reason and renewal_task_histories.note are
    /// varchar(1000), but audit_logs.description is varchar(500) and is
    /// composed as "Renewal task was blocked: {reason}" — a 28-character
    /// prefix. Postgres refuses an over-length value with 22001, and because the
    /// three writes share one SaveChanges, an unbounded reason took the whole
    /// block down as a 500 rather than failing the single field the reader got
    /// wrong. 400 characters keeps every column comfortably inside its limit.
    /// </summary>
    public const int MaxBlockedReasonLength = 400;

    public BlockRenewalTaskCommandValidator()
    {
        // Required, and whitespace is not a reason. This is the same rule the
        // handler enforces inline; it is repeated here so the length rule below
        // can assume a value exists, and so a missing or blank reason is
        // reported as a field-level VALIDATION_ERROR alongside any other bad
        // field rather than as a handler-thrown ArgumentException.
        RuleFor(x => x.BlockedReason)
            .NotEmpty()
            .WithMessage("Blocked reason is required.");

        // Measured on the TRIMMED value, because that is what the handler
        // actually stores: it trims once before assigning. A value of exactly
        // the limit surrounded by spaces must therefore pass, and measuring the
        // raw text would refuse it — the same reasoning that
        // UnblockRenewalTaskCommandHandler documents when it measures the
        // trimmed value.
        RuleFor(x => x.BlockedReason)
            .Must(reason =>
                reason is null ||
                reason.Trim().Length <= MaxBlockedReasonLength)
            .WithMessage(
                $"Blocked reason must not exceed {MaxBlockedReasonLength} characters.");
    }
}
