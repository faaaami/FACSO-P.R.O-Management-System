using FluentValidation;

namespace PROManagementSystem.Application.Documents.Commands.UpdateDocument;

public sealed class UpdateDocumentCommandValidator
    : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .WithMessage("Document ID is required.");

        RuleFor(x => x.DocumentNumber)
            .NotEmpty()
            .WithMessage("Document number is required.")
            .MaximumLength(100)
            .WithMessage("Document number cannot exceed 100 characters.");

        RuleFor(x => x.ExpiryDate)
            .GreaterThanOrEqualTo(x => x.IssueDate)
            .When(x => x.IssueDate.HasValue && x.ExpiryDate.HasValue)
            .WithMessage(
                "Expiry date must be greater than or equal to issue date.");

        RuleFor(x => x.FileName)
            .MaximumLength(255)
            .When(x => x.FileName is not null);

        RuleFor(x => x.ContentType)
            .MaximumLength(100)
            .When(x => x.ContentType is not null);

        RuleFor(x => x.FileUrl)
            .MaximumLength(2000)
            .When(x => x.FileUrl is not null);

        RuleFor(x => x.FileSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.FileSize.HasValue)
            .WithMessage("File size cannot be negative.");
    }
}