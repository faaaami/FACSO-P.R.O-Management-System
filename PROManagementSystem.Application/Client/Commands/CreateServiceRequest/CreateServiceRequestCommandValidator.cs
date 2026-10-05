using FluentValidation;

namespace PROManagementSystem.Application.Client.Commands.CreateServiceRequest;

public sealed class CreateServiceRequestCommandValidator
    : AbstractValidator<CreateServiceRequestCommand>
{
    public CreateServiceRequestCommandValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Type must be a valid service request type.");

        RuleFor(x => x)
            .Must(
                x =>
                    x.EmployeeId.HasValue ||
                    x.EntityId.HasValue)
            .WithMessage(
                "At least one of employeeId or entityId is required.");

        When(
            x => x.EmployeeId.HasValue,
            () => RuleFor(x => x.EmployeeId)
                .NotEmpty()
                .WithMessage("EmployeeId must not be empty."));

        When(
            x => x.EntityId.HasValue,
            () => RuleFor(x => x.EntityId)
                .NotEmpty()
                .WithMessage("EntityId must not be empty."));

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage("Notes must not exceed 2000 characters.");
    }
}