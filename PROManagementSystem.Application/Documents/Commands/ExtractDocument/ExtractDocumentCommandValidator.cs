using FluentValidation;
using PROManagementSystem.Application.Common.FileValidation;

namespace PROManagementSystem.Application.Documents.Commands.ExtractDocument;

public sealed class ExtractDocumentCommandValidator
    : AbstractValidator<ExtractDocumentCommand>
{
    public ExtractDocumentCommandValidator()
    {
        this.ApplyUploadFileRules(
            x => x.FileName,
            x => x.ContentType,
            x => x.FileSize,
            x => x.FileStream);
    }
}