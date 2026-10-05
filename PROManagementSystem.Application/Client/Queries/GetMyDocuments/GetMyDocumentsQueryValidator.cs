using FluentValidation;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocuments;

public sealed class GetMyDocumentsQueryValidator
    : AbstractValidator<GetMyDocumentsQuery>
{
    public GetMyDocumentsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        When(x => x.Type.HasValue,
            () => RuleFor(x => x.Type)
                .IsInEnum());

        When(x => x.Status.HasValue,
            () => RuleFor(x => x.Status)
                .IsInEnum());

        When(x => x.OwnerType.HasValue,
            () => RuleFor(x => x.OwnerType)
                .IsInEnum());

        When(x => x.ExpiresFrom.HasValue && x.ExpiresTo.HasValue,
            () => RuleFor(x => x.ExpiresFrom)
                .LessThanOrEqualTo(x => x.ExpiresTo!.Value)
                .WithMessage("ExpiresFrom must not be after ExpiresTo."));
    }
}