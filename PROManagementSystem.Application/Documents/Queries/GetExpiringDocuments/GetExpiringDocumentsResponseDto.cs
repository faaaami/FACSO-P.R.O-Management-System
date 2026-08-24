using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

public sealed record GetExpiringDocumentsResponseDto(
    List<ExpiringDocumentListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed record ExpiringDocumentListItemDto(
    Guid Id,
    Guid? ClientEntityId,
    Guid? EmployeeId,
    DocumentType Type,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileName,
    bool IsActive,
    bool IsDeleted,
    int DaysRemaining,
    string Status
);