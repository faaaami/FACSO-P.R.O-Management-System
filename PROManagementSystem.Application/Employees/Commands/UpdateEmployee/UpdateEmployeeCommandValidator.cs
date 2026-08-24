using FluentValidation;

namespace PROManagementSystem.Application.Employees.Commands.UpdateEmployee;

public sealed class UpdateEmployeeCommandValidator
    : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty();

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