using FluentValidation;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocuments;

public sealed class GetAgentDocumentsQueryValidator
    : AbstractValidator<GetAgentDocumentsQuery>
{
    public GetAgentDocumentsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}