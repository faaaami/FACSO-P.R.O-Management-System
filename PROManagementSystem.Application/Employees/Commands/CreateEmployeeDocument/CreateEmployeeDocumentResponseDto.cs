using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Employees.Commands.CreateEmployeeDocument;

public sealed record CreateEmployeeDocumentResponseDto(
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
    DateTime CreatedAt,
    object Details
);