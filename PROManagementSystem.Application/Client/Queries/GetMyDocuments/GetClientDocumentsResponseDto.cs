using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocuments;

public sealed record GetClientDocumentsResponseDto(
    List<ClientDocumentListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed record ClientDocumentListItemDto(
    Guid Id,
    Guid? ClientEntityId,
    Guid? EmployeeId,
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