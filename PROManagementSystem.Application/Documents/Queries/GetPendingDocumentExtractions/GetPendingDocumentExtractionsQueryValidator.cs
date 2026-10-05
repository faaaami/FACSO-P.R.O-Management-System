using FluentValidation;

namespace PROManagementSystem.Application.Documents.Queries.GetPendingDocumentExtractions;

public sealed class GetPendingDocumentExtractionsQueryValidator
    : AbstractValidator<GetPendingDocumentExtractionsQuery>
{
    public GetPendingDocumentExtractionsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        // Status is a raw string column, so an unrecognised value would
        // silently match nothing. Reject it up front instead.
        RuleFor(x => x.Status)
            .Must(status =>
                string.IsNullOrWhiteSpace(status) ||
                DocumentExtractionStatusVocabulary.All.Contains(
                    status.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            .WithMessage(
                "Status must be one of: Pending, Confirmed, Expired.");
    }
}
