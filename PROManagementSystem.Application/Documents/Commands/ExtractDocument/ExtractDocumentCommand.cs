using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Commands.ExtractDocument;

/// <param name="DocumentType">Chosen by staff before upload.</param>
public sealed record ExtractDocumentCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSize,
    DocumentType DocumentType
) : IRequest<ExtractDocumentResponseDto>;
