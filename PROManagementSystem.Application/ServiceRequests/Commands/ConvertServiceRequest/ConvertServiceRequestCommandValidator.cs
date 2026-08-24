using FluentValidation;

namespace PROManagementSystem.Application.ServiceRequests.Commands.ConvertServiceRequest;

public sealed class ConvertServiceRequestCommandValidator
    : AbstractValidator<ConvertServiceRequestCommand>
{
    public ConvertServiceRequestCommandValidator()
    {
        RuleFor(x => x.ServiceRequestId)
            .NotEmpty()
            .WithMessage("Service request ID is required.");
    }
}
