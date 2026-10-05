using FluentValidation;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Validation;

namespace PROManagementSystem.Application.Documents.Commands.ConfirmDocument;

public sealed class ConfirmDocumentCommandValidator
    : AbstractValidator<ConfirmDocumentCommand>
{
    private readonly DocumentDetailsValidator _detailsValidator;

    public ConfirmDocumentCommandValidator(
        DocumentDetailsValidator detailsValidator)
    {
        _detailsValidator = detailsValidator;

        RuleFor(x => x.DocumentNumber)
            .NotEmpty()
            .MaximumLength(100)
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage(
                "Document number is required and must be at most 100 characters.");

        RuleFor(x => x)
            .Must(x =>
                x.EmployeeId.HasValue != x.ClientEntityId.HasValue)
            .WithMessage(
                "Exactly one of EmployeeId or ClientEntityId must be provided.")
            .WithName("Owner");

        RuleFor(x => x.ExpiryDate)
            .GreaterThanOrEqualTo(x => x.IssueDate)
            .When(x => x.IssueDate.HasValue && x.ExpiryDate.HasValue)
            .WithMessage(
                "Expiry date must be greater than or equal to issue date.");

        RuleFor(x => x.FileName)
            .MaximumLength(255)
            .When(x => x.FileName is not null)
            .WithMessage("File name must be at most 255 characters.");

        RuleFor(x => x.ContentType)
            .MaximumLength(100)
            .When(x => x.ContentType is not null)
            .WithMessage("Content type must be at most 100 characters.");

        RuleFor(x => x.FileUrl)
            .MaximumLength(1000)
            .When(x => x.FileUrl is not null)
            .WithMessage("File URL must be at most 1000 characters.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0)
            .When(x => x.FileSize.HasValue)
            .WithMessage("File size must be greater than 0.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Document type is not a defined value.");

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
                    validateRequired: true))
                {
                    context.AddFailure(failure);
                }
            });
    }
}