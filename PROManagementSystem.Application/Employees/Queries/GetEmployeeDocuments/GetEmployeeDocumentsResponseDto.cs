using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

public sealed record GetEmployeeDocumentsResponseDto(
    List<EmployeeDocumentListItemDto> Items,
    int TotalCount
);

public sealed record EmployeeDocumentListItemDto(
    Guid Id,
    Guid EmployeeId,
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