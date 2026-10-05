using FluentValidation;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Queries.GetMyRenewalTasks;

public sealed class GetMyRenewalTasksQueryValidator
    : AbstractValidator<GetMyRenewalTasksQuery>
{
    public GetMyRenewalTasksQueryValidator()
    {
        When(x => x.Status.HasValue,
            () => RuleFor(x => x.Status)
                .IsInEnum());

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}