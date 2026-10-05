using FluentValidation;

namespace PROManagementSystem.Application.Clients.Commands.ApproveClient;

public sealed class ApproveClientCommandValidator
    : AbstractValidator<ApproveClientCommand>
{
    public ApproveClientCommandValidator()
    {
        RuleFor(x => x.ClientCompanyId)
            .NotEmpty();

        RuleFor(x => x.ContactId)
            .NotEmpty();
    }
}