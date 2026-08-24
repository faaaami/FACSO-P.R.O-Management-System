using MediatR;

namespace PROManagementSystem.Application.Documents.Commands.CreateDocumentVersion;

public sealed record CreateDocumentVersionCommand(
    Guid DocumentId,
    string FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize
) : IRequest<CreateDocumentVersionResponseDto>;