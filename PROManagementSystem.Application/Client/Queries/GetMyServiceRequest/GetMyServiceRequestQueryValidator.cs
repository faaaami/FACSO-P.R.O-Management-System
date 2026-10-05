using FluentValidation;

namespace PROManagementSystem.Application.Client.Queries.GetMyServiceRequest;

public sealed class GetMyServiceRequestQueryValidator
    : AbstractValidator<GetMyServiceRequestQuery>
{
    public GetMyServiceRequestQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}