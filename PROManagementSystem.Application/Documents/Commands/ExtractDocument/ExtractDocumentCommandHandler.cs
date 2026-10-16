using MediatR;
using PROManagementSystem.Application.Documents.Common;

namespace PROManagementSystem.Application.Documents.Commands.ExtractDocument;

public sealed class ExtractDocumentCommandHandler
    : IRequestHandler<ExtractDocumentCommand, ExtractDocumentResponseDto>
{
    private readonly ExtractionDraftCreator _drafts;

    public ExtractDocumentCommandHandler(ExtractionDraftCreator drafts)
    {
        _drafts = drafts;
    }

    public Task<ExtractDocumentResponseDto> Handle(
        ExtractDocumentCommand request,
        CancellationToken cancellationToken)
        => _drafts.CreateAsync(
            new ExtractionUpload(
                request.FileStream,
                request.FileName,
                request.ContentType,
                request.FileSize,
                request.DocumentType),
            createdBy: null,
            cancellationToken);
}
