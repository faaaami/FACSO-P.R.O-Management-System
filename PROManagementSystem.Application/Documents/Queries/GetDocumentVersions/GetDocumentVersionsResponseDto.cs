namespace PROManagementSystem.Application.Documents.Queries.GetDocumentVersions;

public sealed record GetDocumentVersionsResponseDto(
    List<DocumentVersionListItemDto> Items,
    int TotalCount
);

public sealed record DocumentVersionListItemDto(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    string FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt
);