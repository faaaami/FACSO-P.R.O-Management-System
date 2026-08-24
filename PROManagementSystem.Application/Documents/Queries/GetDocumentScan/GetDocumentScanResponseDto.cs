namespace PROManagementSystem.Application.Documents.Queries.GetDocumentScan;

public sealed record GetDocumentScanResponseDto(
    Guid DocumentId,
    string FileUrl,
    string? FileName,
    string? ContentType
);