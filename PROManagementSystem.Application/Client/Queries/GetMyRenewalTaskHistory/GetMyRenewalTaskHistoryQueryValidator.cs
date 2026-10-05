using FluentValidation;

namespace PROManagementSystem.Application.Client.Queries.GetMyRenewalTaskHistory;

public sealed class GetMyRenewalTaskHistoryQueryValidator
    : AbstractValidator<GetMyRenewalTaskHistoryQuery>
{
    public GetMyRenewalTaskHistoryQueryValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty()
            .WithMessage("TaskId is required.");
    }
}