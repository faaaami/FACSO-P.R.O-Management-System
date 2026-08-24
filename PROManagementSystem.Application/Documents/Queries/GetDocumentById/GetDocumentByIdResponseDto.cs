using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentById;

public sealed record GetDocumentByIdResponseDto(
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
    DateTime? DeletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DocumentStatus Status
);