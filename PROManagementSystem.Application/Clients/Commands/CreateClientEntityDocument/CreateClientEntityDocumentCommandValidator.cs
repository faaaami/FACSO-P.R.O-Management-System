using FluentValidation;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Validation;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientEntityDocument;

public sealed class CreateClientEntityDocumentCommandValidator
    : AbstractValidator<CreateClientEntityDocumentCommand>
{
    private readonly DocumentDetailsValidator _detailsValidator;

    public CreateClientEntityDocumentCommandValidator(
        DocumentDetailsValidator detailsValidator)
    {
        _detailsValidator = detailsValidator;

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