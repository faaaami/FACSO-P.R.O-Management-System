using FluentValidation;

namespace PROManagementSystem.Application.Employees.Queries.GetExpiringEmployees;

public sealed class GetExpiringEmployeesQueryValidator
    : AbstractValidator<GetExpiringEmployeesQuery>
{
    public GetExpiringEmployeesQueryValidator()
    {
        RuleFor(x => x.Days)
            .InclusiveBetween(1, 365)
            .WithMessage(
                "Days must be between 1 and 365.");

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