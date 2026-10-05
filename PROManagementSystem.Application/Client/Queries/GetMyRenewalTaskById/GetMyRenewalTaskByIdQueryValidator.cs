using FluentValidation;

namespace PROManagementSystem.Application.Client.Queries.GetMyRenewalTaskById;

public sealed class GetMyRenewalTaskByIdQueryValidator
    : AbstractValidator<GetMyRenewalTaskByIdQuery>
{
    public GetMyRenewalTaskByIdQueryValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty()
            .WithMessage("TaskId is required.");
    }
}