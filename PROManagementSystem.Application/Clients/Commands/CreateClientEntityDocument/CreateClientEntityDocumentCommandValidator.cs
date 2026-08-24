using FluentValidation;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientEntityDocument;

public sealed class CreateClientEntityDocumentCommandValidator
    : AbstractValidator<CreateClientEntityDocumentCommand>
{
    public CreateClientEntityDocumentCommandValidator()
    {
        RuleFor(x => x.ClientEntityId)
            .NotEmpty();

        RuleFor(x => x.DocumentNumber)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.ExpiryDate)
            .GreaterThan(x => x.IssueDate)
            .When(x =>
                x.IssueDate.HasValue &&
                x.ExpiryDate.HasValue)
            .WithMessage(
                "Expiry date must be after issue date.");

        RuleFor(x => x.FileName)
            .MaximumLength(255);

        RuleFor(x => x.ContentType)
            .MaximumLength(100);

        RuleFor(x => x.FileUrl)
            .MaximumLength(1000);

        RuleFor(x => x.FileSize)
            .GreaterThan(0)
            .When(x => x.FileSize.HasValue);
    }
}