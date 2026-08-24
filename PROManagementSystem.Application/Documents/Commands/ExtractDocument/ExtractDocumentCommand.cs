using MediatR;

namespace PROManagementSystem.Application.Documents.Commands.ExtractDocument;

public sealed record ExtractDocumentCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSize
) : IRequest<ExtractDocumentResponseDto>;