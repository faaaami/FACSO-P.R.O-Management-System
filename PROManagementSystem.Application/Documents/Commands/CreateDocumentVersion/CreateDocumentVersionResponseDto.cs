namespace PROManagementSystem.Application.Documents.Commands.CreateDocumentVersion;

public sealed record CreateDocumentVersionResponseDto(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    string FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    DateTime CreatedAt
);