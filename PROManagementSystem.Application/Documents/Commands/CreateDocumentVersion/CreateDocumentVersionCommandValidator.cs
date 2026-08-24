using FluentValidation;

namespace PROManagementSystem.Application.Documents.Commands.CreateDocumentVersion;

public sealed class CreateDocumentVersionCommandValidator
    : AbstractValidator<CreateDocumentVersionCommand>
{
    public CreateDocumentVersionCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .WithMessage("Document ID is required.");

        RuleFor(x => x.FileUrl)
            .NotEmpty()
            .WithMessage("File URL is required.")
            .MaximumLength(1000)
            .WithMessage("File URL cannot exceed 1000 characters.");

        RuleFor(x => x.FileName)
            .MaximumLength(255)
            .When(x => x.FileName is not null);

        RuleFor(x => x.ContentType)
            .MaximumLength(100)
            .When(x => x.ContentType is not null);

        RuleFor(x => x.FileSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.FileSize.HasValue)
            .WithMessage("File size cannot be negative.");
    }
}