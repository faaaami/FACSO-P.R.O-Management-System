using FluentValidation;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Queries.GetMyServiceRequests;

public sealed class GetMyServiceRequestsQueryValidator
    : AbstractValidator<GetMyServiceRequestsQuery>
{
    public GetMyServiceRequestsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        When(x => x.Status.HasValue,
            () => RuleFor(x => x.Status)
                .IsInEnum());
    }
}