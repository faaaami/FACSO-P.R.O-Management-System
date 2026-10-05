using FluentValidation;
using PROManagementSystem.Application.Documents.Queries.GetPendingDocumentExtractions;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentPendingDocumentExtractions;

public sealed class GetAgentPendingDocumentExtractionsQueryValidator
    : AbstractValidator<GetAgentPendingDocumentExtractionsQuery>
{
    public GetAgentPendingDocumentExtractionsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

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
