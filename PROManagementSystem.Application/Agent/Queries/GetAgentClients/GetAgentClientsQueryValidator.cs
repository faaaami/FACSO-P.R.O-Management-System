using FluentValidation;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentClients;

public sealed class GetAgentClientsQueryValidator
    : AbstractValidator<GetAgentClientsQuery>
{
    public GetAgentClientsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}