using FluentValidation;

namespace PROManagementSystem.Application.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeCommandValidator
    : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.ClientEntityId)
            .NotEmpty()
            .WithMessage("Client entity is required.");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PassportNumber)
            .MaximumLength(50);

        RuleFor(x => x.Nationality)
            .MaximumLength(100);

        RuleFor(x => x.JobTitle)
            .MaximumLength(150);

        RuleFor(x => x.HireDate)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage(
                "Hire date cannot be in the future.");
    }
}