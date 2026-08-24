using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;

public sealed record GetDocumentDependenciesResponseDto(
    Guid DocumentId,
    IReadOnlyList<DocumentDependencyItemDto> Items,
    int TotalCount
);

public sealed record DocumentDependencyItemDto(
    Guid Id,
    Guid DependsOnDocumentId,
    DocumentType DocumentType,
    string DocumentNumber,
    DateTime? ExpiryDate,
    bool IsActive,
    bool IsDeleted
);