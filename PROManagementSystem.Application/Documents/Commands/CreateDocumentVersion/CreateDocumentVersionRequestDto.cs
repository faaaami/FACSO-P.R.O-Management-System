namespace PROManagementSystem.Application.Documents.Commands.CreateDocumentVersion;

public sealed record CreateDocumentVersionRequestDto(
    string FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize
);