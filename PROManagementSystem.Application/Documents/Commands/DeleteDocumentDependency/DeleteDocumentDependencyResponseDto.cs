namespace PROManagementSystem.Application.Documents.Commands.DeleteDocumentDependency;

public sealed record DeleteDocumentDependencyResponseDto(
    Guid Id,
    bool IsDeleted,
    DateTime DeletedAt
);