using FluentValidation;

namespace PROManagementSystem.Application.ServiceRequests.Commands.RejectServiceRequest;

public sealed class RejectServiceRequestCommandValidator
    : AbstractValidator<RejectServiceRequestCommand>
{
    public RejectServiceRequestCommandValidator()
    {
        RuleFor(x => x.ServiceRequestId)
            .NotEmpty()
            .WithMessage("Service request ID is required.");

        // NotEmpty() alone lets a whitespace-only reason through, which the
        // handler then trims to an empty string. The client would see a
        // rejection with no explanation at all, so require real content.
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Rejection reason is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Rejection reason is required.")
            .MaximumLength(1000)
            .WithMessage("Rejection reason must not exceed 1000 characters.");
    }
}
