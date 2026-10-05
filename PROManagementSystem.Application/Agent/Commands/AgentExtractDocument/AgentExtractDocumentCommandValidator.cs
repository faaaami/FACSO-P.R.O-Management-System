using FluentValidation;
using PROManagementSystem.Application.Common.FileValidation;

namespace PROManagementSystem.Application.Agent.Commands.AgentExtractDocument;

public sealed class AgentExtractDocumentCommandValidator
    : AbstractValidator<AgentExtractDocumentCommand>
{
    public AgentExtractDocumentCommandValidator()
    {
        this.ApplyUploadFileRules(
            x => x.FileName,
            x => x.ContentType,
            x => x.FileSize,
            x => x.FileStream);
    }
}