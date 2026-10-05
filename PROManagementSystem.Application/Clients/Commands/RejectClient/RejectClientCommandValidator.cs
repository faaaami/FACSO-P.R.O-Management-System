using FluentValidation;

namespace PROManagementSystem.Application.Clients.Commands.RejectClient;

public sealed class RejectClientCommandValidator
    : AbstractValidator<RejectClientCommand>
{
    public RejectClientCommandValidator()
    {
        RuleFor(x => x.ClientCompanyId)
            .NotEmpty();

        RuleFor(x => x.ContactId)
            .NotEmpty();
    }
}