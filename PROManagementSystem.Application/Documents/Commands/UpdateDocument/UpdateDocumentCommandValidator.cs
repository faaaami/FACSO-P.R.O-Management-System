using FluentValidation;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Validation;

namespace PROManagementSystem.Application.Documents.Commands.UpdateDocument;

public sealed class UpdateDocumentCommandValidator
    : AbstractValidator<UpdateDocumentCommand>
{
    private readonly DocumentDetailsValidator _detailsValidator;

    public UpdateDocumentCommandValidator(
        DocumentDetailsValidator detailsValidator)
    {
        _detailsValidator = detailsValidator;

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

        RuleFor(x => x.Details)
            .Must(DetailsJson.IsObjectOrNull)
            .WithMessage("Details must be a JSON object or null.");

        RuleFor(x => x.Details)
            .Custom((details, context) =>
            {
                var command = context.InstanceToValidate;

                foreach (var failure in _detailsValidator.Validate(
                    command.Type,
                    details,
                    validateRequired: false))
                {
                    context.AddFailure(failure);
                }
            });
    }
}