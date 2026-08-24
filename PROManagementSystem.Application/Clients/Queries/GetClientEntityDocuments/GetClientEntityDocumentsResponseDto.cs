using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Queries.GetClientEntityDocuments;

public sealed record GetClientEntityDocumentsResponseDto(
    List<ClientEntityDocumentListItemDto> Items,
    int TotalCount
);

public sealed record ClientEntityDocumentListItemDto(
    Guid Id,
    Guid ClientEntityId,
    DocumentType Type,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DocumentStatus Status
);