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

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Rejection reason is required.")
            .MaximumLength(1000)
            .WithMessage("Rejection reason must not exceed 1000 characters.");
    }
}
