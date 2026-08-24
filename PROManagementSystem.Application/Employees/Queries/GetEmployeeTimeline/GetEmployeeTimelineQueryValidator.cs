using FluentValidation;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeTimeline;

public sealed class GetEmployeeTimelineQueryValidator
    : AbstractValidator<GetEmployeeTimelineQuery>
{
    public GetEmployeeTimelineQueryValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty()
            .WithMessage(
                "Employee ID is required.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage(
                "Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage(
                "Page size must be between 1 and 100.");
    }
}