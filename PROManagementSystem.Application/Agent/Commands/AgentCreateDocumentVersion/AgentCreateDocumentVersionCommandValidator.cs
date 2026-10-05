using FluentValidation;

namespace PROManagementSystem.Application.Agent.Commands.AgentCreateDocumentVersion;

public sealed class AgentCreateDocumentVersionCommandValidator
    : AbstractValidator<AgentCreateDocumentVersionCommand>
{
    public AgentCreateDocumentVersionCommandValidator()
    {
        RuleFor(x => x.FileUrl)
            .NotEmpty()
            .MaximumLength(1000)
            .WithMessage(
                "File URL is required and must be at most 1000 characters.");

        RuleFor(x => x.FileName)
            .MaximumLength(255)
            .When(x => x.FileName is not null)
            .WithMessage("File name must be at most 255 characters.");

        RuleFor(x => x.ContentType)
            .MaximumLength(100)
            .When(x => x.ContentType is not null)
            .WithMessage("Content type must be at most 100 characters.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0)
            .When(x => x.FileSize.HasValue)
            .WithMessage("File size must be greater than 0.");
    }
}