using FluentValidation;

namespace PROManagementSystem.Application.Documents.Commands.CreateDocumentDependency;

public sealed class CreateDocumentDependencyCommandValidator
    : AbstractValidator<CreateDocumentDependencyCommand>
{
    public CreateDocumentDependencyCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .WithMessage("Document ID is required.");

        RuleFor(x => x.DependsOnDocumentId)
            .NotEmpty()
            .WithMessage(
                "Dependency document ID is required.");

        RuleFor(x => x)
            .Must(x =>
                x.DocumentId != x.DependsOnDocumentId)
            .WithMessage(
                "A document cannot depend on itself.");
    }
}