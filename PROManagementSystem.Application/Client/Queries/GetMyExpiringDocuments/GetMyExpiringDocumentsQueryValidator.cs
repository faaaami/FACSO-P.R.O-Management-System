using FluentValidation;

namespace PROManagementSystem.Application.Client.Queries.GetMyExpiringDocuments;

public sealed class GetMyExpiringDocumentsQueryValidator
    : AbstractValidator<GetMyExpiringDocumentsQuery>
{
    public GetMyExpiringDocumentsQueryValidator()
    {
        RuleFor(x => x.Days)
            .Must(x => x is 30 or 60 or 90)
            .WithMessage("Days must be one of 30, 60 or 90.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}