namespace PROManagementSystem.Application.Documents.Commands.DeleteDocument;

public sealed record DeleteDocumentResponseDto(
    Guid Id,
    bool IsDeleted,
    bool IsActive,
    DateTime DeletedAt
);